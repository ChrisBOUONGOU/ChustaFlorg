using System.Text.RegularExpressions;

namespace ChustaFlorg.Services;

public static partial class ChordTransposer
{
    private static readonly string[] Sharp=["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"];
    private static readonly string[] Flat=["C","Db","D","Eb","E","F","Gb","G","Ab","A","Bb","B"];
    [GeneratedRegex(@"(?<![A-Za-z])([A-G])([#b]?)(m|maj|min|dim|aug|sus|add)?(\d{0,2})?(?:/([A-G])([#b]?))?")]
    private static partial Regex ChordPattern();
    public static string Transpose(string source,int semitones)
    {
        if(semitones==0||string.IsNullOrEmpty(source))return source;
        return ChordPattern().Replace(source,m=>
        {
            var root=Shift(m.Groups[1].Value+m.Groups[2].Value,semitones);
            var quality=m.Groups[3].Value+m.Groups[4].Value;
            var bass=m.Groups[5].Success?"/"+Shift(m.Groups[5].Value+m.Groups[6].Value,semitones):string.Empty;
            return root+quality+bass;
        });
    }
    private static string Shift(string note,int amount)
    {
        var normalized=note.Replace("Db","C#").Replace("Eb","D#").Replace("Gb","F#").Replace("Ab","G#").Replace("Bb","A#");
        var index=Array.IndexOf(Sharp,normalized);if(index<0)return note;
        var shifted=(index+amount%12+12)%12;return note.Contains('b')?Flat[shifted]:Sharp[shifted];
    }
}
