function Resolve-ContextName {
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Context
    )

    # Aliases: da/data, id/identity, ap/app/application
    switch -Regex ($Context.ToLower()) {
        '^(da|data)$'               { return 'DataDbContext' }
        '^(id|identity)$'           { return 'IdentityDbContext' }
        '^(ap|app|application)$'    { return 'ApplicationDbContext' }
        default                     { return $Context }
    }
}

function Add-Mig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Target,

        [Parameter(Mandatory = $true, Position = 1)]
        [string]$Name
    )

    $contextName = Resolve-ContextName $Target

    switch ($contextName) {
        'DataDbContext'        { Add-Migration $Name -Context $contextName -OutputDir 'Persistence/Migrations/Data'; break }
        'IdentityDbContext'    { Add-Migration $Name -Context $contextName -OutputDir 'Persistence/Migrations/Identity'; break }
        'ApplicationDbContext' { Add-Migration $Name -Context $contextName -OutputDir 'Persistence/Migrations/App'; break }
        default                { Add-Migration $Name -Context $contextName; break }
    }
}

Set-Alias addmig Add-Mig

function Up-Db {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Target
    )

    $contextName = Resolve-ContextName $Target
    Update-Database -Context $contextName
}

Set-Alias updb Up-Db
