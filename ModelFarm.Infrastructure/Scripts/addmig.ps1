param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$Name
)

$ErrorActionPreference = 'Stop'

# Aliases: da/data, id/identity, ap/app/application
switch -Regex ($Target.ToLower()) {
    '^(da|data)$'               { Add-Migration $Name -Context DataDbContext -OutputDir 'Persistence/Migrations/Data'; break }
    '^(id|identity)$'           { Add-Migration $Name -Context IdentityDbContext -OutputDir 'Persistence/Migrations/Identity'; break }
    '^(ap|app|application)$'    { Add-Migration $Name -Context ApplicationDbContext -OutputDir 'Persistence/Migrations/App'; break }
    default                     { Add-Migration $Name -Context $Target; break }
}
