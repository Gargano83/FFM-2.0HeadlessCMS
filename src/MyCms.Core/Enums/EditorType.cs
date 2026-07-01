namespace MyCms.Core.Enums;

/// <summary>
/// Determina quale editor template Razor viene usato per renderizzare
/// un campo nelle view di CRUD generico (list/create/edit).
/// </summary>
public enum EditorType
{
    Text = 0,
    TextArea = 1,
    RichText = 2,
    Number = 3,
    Checkbox = 4,
    Date = 5,
    DateTime = 6,
    Select = 7,
    File = 8,
    Hidden = 9
}
