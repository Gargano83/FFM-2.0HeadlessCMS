using DAMIHeadlessCMS.Core.Enums;

namespace DAMIHeadlessCMS.Scaffolding;

/// <summary>
/// Deduce l'EditorType (quale controllo Razor usare) a partire dal tipo SQL
/// nativo di una colonna. Il risultato è solo un default: nel wizard l'utente
/// potrà sempre correggerlo manualmente prima di salvare.
/// </summary>
public static class EditorTypeInferrer
{
    public static EditorType Infer(string sqlDataType, bool isForeignKey, int? maxLength)
    {
        if (isForeignKey)
        {
            return EditorType.Select;
        }

        return sqlDataType.ToLowerInvariant() switch
        {
            "bit" => EditorType.Checkbox,

            "date" => EditorType.Date,
            "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" => EditorType.DateTime,

            "int" or "bigint" or "smallint" or "tinyint"
                or "decimal" or "numeric" or "float" or "real"
                or "money" or "smallmoney" => EditorType.Number,

            "varchar" or "nvarchar" or "char" or "nchar" =>
                maxLength is null or > 200 ? EditorType.TextArea : EditorType.Text,

            "text" or "ntext" => EditorType.TextArea,

            "varbinary" or "binary" or "image" => EditorType.File,

            "uniqueidentifier" => EditorType.Text,

            _ => EditorType.Text
        };
    }
}
