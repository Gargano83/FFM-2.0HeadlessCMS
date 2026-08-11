namespace DAMIHeadlessCMS.Core.Enums;

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
    Hidden = 9,

    /// <summary>
    /// Campo password: nel form si presenta come &lt;input type="password"&gt;
    /// mai precompilato (l'hash esistente non viene mai rimandato al browser).
    /// Lasciato vuoto in modifica, il valore esistente non viene toccato. Se
    /// valorizzato, il testo in chiaro viene trasformato prima della scrittura
    /// su database — vedi <see cref="Entities.FieldDefinition.PasswordHashFunction"/>.
    /// </summary>
    Password = 10
}
