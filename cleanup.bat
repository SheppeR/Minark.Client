@echo off
echo Nettoyage des dossiers bin et obj...

for %%P in (
    Minark.Client
    Minark.Game.Shared
    Minark.GameServer
    Minark.Packager
    Minark.Server
    Minark.Shared
) do (
    if exist "%%P\bin" (
        rd /s /q "%%P\bin"
        echo Supprime : %%P\bin
    )
    if exist "%%P\obj" (
        rd /s /q "%%P\obj"
        echo Supprime : %%P\obj
    )
)

echo.
echo Nettoyage termine !
pause