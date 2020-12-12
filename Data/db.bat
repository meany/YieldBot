REM dev commands
dotnet ef migrations add init
dotnet ef database update
dotnet ef database update 0
dotnet ef migrations remove

REM production script
dotnet ef migrations script --idempotent --output "script.sql"