# DAMIHeadlessCMS.Core

Entità di dominio POCO (`EntityDefinition`, `FieldDefinition`, `CmsPage`,
`CmsMenu`, `CmsMenuItem`, `LocalizationSource`) ed enum (`EditorType`,
`MenuTargetType`) del CMS headless **DAMIHeadlessCMS**. Nessuna dipendenza
da EF Core: riutilizzabile anche da un eventuale layer di servizi/API.

Fa parte della suite DAMIHeadlessCMS insieme a `DAMIHeadlessCMS.Data`,
`DAMIHeadlessCMS.Scaffolding` e `DAMIHeadlessCMS.Admin`. Per la
documentazione completa, l'architettura e la guida all'integrazione in
un'applicazione host, vedi il README del repository.
