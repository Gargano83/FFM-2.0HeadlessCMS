# DAMIHeadlessCMS.Scaffolding

Lettura dello schema del database via query T-SQL dirette su `sys.*`
(`SqlServerSchemaReader`), inferenza dell'`EditorType` a partire dal tipo
di colonna (`EditorTypeInferrer`) e orchestrazione dello scaffold
idempotente (`ScaffoldingService`) del CMS headless **DAMIHeadlessCMS**.

Fa parte della suite DAMIHeadlessCMS insieme a `DAMIHeadlessCMS.Core`,
`DAMIHeadlessCMS.Data` e `DAMIHeadlessCMS.Admin`. Per la documentazione
completa, l'architettura e la guida all'integrazione in un'applicazione
host, vedi il README del repository.
