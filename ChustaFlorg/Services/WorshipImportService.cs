using System.Text;
using System.Text.Json;
using ChustaFlorg.Models;

namespace ChustaFlorg.Services;

public sealed class WorshipImportService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public async Task<List<WorshipSong>> ImportAsync(Stream stream, string fileName, CancellationToken token = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".json")
            return await JsonSerializer.DeserializeAsync<List<WorshipSong>>(stream, Options, token) ?? [];
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var text = await reader.ReadToEndAsync(token);
        return ext switch { ".csv" => ParseCsv(text), ".txt" => ParseText(text), _ => throw new NotSupportedException("Formats acceptés : JSON, CSV et TXT.") };
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var b = new StringBuilder();
        var previousWasSeparator = true;
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
            {
                b.Append(c);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                b.Append(' ');
                previousWasSeparator = true;
            }
        }
        return b.ToString().Trim();
    }

    public static List<(WorshipSong First, WorshipSong Second)> FindDuplicates(IEnumerable<WorshipSong> source)
    {
        var songs = source.ToList();
        var pairs = new List<(WorshipSong, WorshipSong)>();
        for (var i=0;i<songs.Count;i++) for(var j=i+1;j<songs.Count;j++)
        {
            var a=Normalize(songs[i].Title); var b=Normalize(songs[j].Title);
            if (a.Length>2 && a==b) { pairs.Add((songs[i],songs[j])); continue; }
            var wa=Normalize(string.Join(" ",songs[i].Sections.Select(x=>x.Lyrics))).Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var wb=Normalize(string.Join(" ",songs[j].Sections.Select(x=>x.Lyrics))).Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var union=wa.Union(wb).Count();
            if(wa.Count>=5 && wb.Count>=5 && union>0 && (double)wa.Intersect(wb).Count()/union>=0.88) pairs.Add((songs[i],songs[j]));
        }
        return pairs;
    }

    private static List<WorshipSong> ParseCsv(string text)
    {
        var rows=ReadCsv(text).ToList(); if(rows.Count<2) return [];
        var headers=rows[0].Select(Normalize).ToList();
        string Get(List<string> row,string key){var i=headers.IndexOf(Normalize(key));return i>=0&&i<row.Count?row[i]:string.Empty;}
        var songs=new Dictionary<string,WorshipSong>();
        foreach(var row in rows.Skip(1))
        {
            var title=Get(row,"Title"); if(string.IsNullOrWhiteSpace(title)) continue;
            var key=Normalize(title)+"|"+Normalize(Get(row,"Author"));
            if(!songs.TryGetValue(key,out var song))
            {
                song=new WorshipSong { Title=title.Trim(), Author=Get(row,"Author").Trim(), Key=Get(row,"Key").Trim(),
                    Tempo=int.TryParse(Get(row,"Tempo"),out var bpm)?Math.Clamp(bpm,0,400):0,
                    Tags=Get(row,"Tags").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList() };
                songs[key]=song;
            }
            if(Enum.TryParse<SongSectionType>(Get(row,"SectionType"),true,out var type))
                song.Sections.Add(new SongSection { Type=type, Number=int.TryParse(Get(row,"SectionNumber"),out var n)?Math.Max(1,n):1, Lyrics=Get(row,"Lyrics"), ChordLyrics=Get(row,"Chords") });
        }
        return songs.Values.ToList();
    }

    private static List<WorshipSong> ParseText(string text)
    {
        var result=new List<WorshipSong>(); WorshipSong? song=null; SongSection? section=null; var lyrics=new StringBuilder();
        void FlushSection(){if(section is null)return;section.Lyrics=lyrics.ToString().Trim();song!.Sections.Add(section);lyrics.Clear();section=null;}
        void FlushSong(){FlushSection();if(song is not null&&!string.IsNullOrWhiteSpace(song.Title))result.Add(song);}
        foreach(var raw in text.Replace("\r",string.Empty).Split('\n'))
        {
            var line=raw.TrimEnd();
            if(line.StartsWith("# ")){FlushSong();song=new WorshipSong{Title=line[2..].Trim()};}
            else if(song is null){if(!string.IsNullOrWhiteSpace(line))song=new WorshipSong{Title=line.Trim()};}
            else if(line.StartsWith("@author:",StringComparison.OrdinalIgnoreCase))song.Author=line[8..].Trim();
            else if(line.StartsWith("@key:",StringComparison.OrdinalIgnoreCase))song.Key=line[5..].Trim();
            else if(line.StartsWith("@tempo:",StringComparison.OrdinalIgnoreCase))song.Tempo=int.TryParse(line[7..].Trim(),out var bpm)?Math.Clamp(bpm,0,400):0;
            else if(line.StartsWith("[")&&line.EndsWith("]")&&Enum.TryParse<SongSectionType>(line[1..^1].Split(' ')[0],true,out var type))
            {FlushSection();var parts=line[1..^1].Split(' ');section=new SongSection{Type=type,Number=int.TryParse(parts.Last(),out var n)?Math.Max(1,n):1};}
            else if(section is not null)lyrics.AppendLine(line);
        }
        FlushSong();return result;
    }

    private static IEnumerable<List<string>> ReadCsv(string text)
    {
        var rows=new List<string>();var field=new StringBuilder();var quoted=false;
        for(var i=0;i<text.Length;i++){var c=text[i];
            if(c=='"'&&quoted&&i+1<text.Length&&text[i+1]=='"'){field.Append('"');i++;continue;}
            if(c=='"'){quoted=!quoted;continue;}
            if(c==','&&!quoted){rows.Add(field.ToString());field.Clear();continue;}
            if((c=='\n'||c=='\r')&&!quoted){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;rows.Add(field.ToString());field.Clear();if(rows.Any(x=>!string.IsNullOrWhiteSpace(x)))yield return rows;rows=[];continue;}
            field.Append(c);
        }
        rows.Add(field.ToString());if(rows.Any(x=>!string.IsNullOrWhiteSpace(x)))yield return rows;
    }
}
