param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Target
)

$ErrorActionPreference = 'Stop'

# Aliases: da/data, id/identity, ap/app/application
switch -Regex ($Target.ToLower()) {
    '^(da|data)$'               { Update-Database -Context DataDbContext; break }
    '^(id|identity)$'           { Update-Database -Context IdentityDbContext; break }
    '^(ap|app|application)$'    { Update-Database -Context ApplicationDbContext; break }
    default                     { Update-Database -Context $Target; break }
}
