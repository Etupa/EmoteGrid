namespace EmoteGrid.Models;

public class EmoteData {
    public ushort Id;
    public string Name = "";
    public uint IconId;
    public uint Category;
    public Lumina.Excel.Sheets.Emote EmoteSheetData;
    public Dalamud.Interface.Textures.ISharedImmediateTexture? SharedTexture;
    public bool IconLoadFailed;
}
