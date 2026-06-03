# Logs de diagnostico

O MPV.NET Media Player possui um logger interno simples para diagnostico de suporte.
Ele grava arquivos diarios somente quando o build e gerado com logging habilitado.

## Estado padrao

Logs em arquivo ficam desabilitados por padrao.

Use esse padrao para releases publicas:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=false
```

O mesmo padrao e usado pelos scripts de release quando nenhum parametro extra e informado.

## Como habilitar

Para gerar uma versao de diagnostico com logs em arquivo:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=true
```

Nos scripts do fork, use `-EnableFileLogging`:

```powershell
.\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
.\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
.\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -EnableFileLogging
```

No workflow manual `.github/workflows/release-packages.yml`, selecione
`enable_file_logging=true` apenas para pacotes de diagnostico/suporte.

## Onde os logs ficam

Quando habilitado, o aplicativo grava em:

```text
%LOCALAPPDATA%\mpv.net\Logs
```

O arquivo diario usa este padrao:

```text
mpvnet-YYYY-MM-DD.log
```

Exemplo:

```text
mpvnet-2026-06-02.log
```

Cada entrada contem data/hora completa, nivel e mensagem. Quando existe excecao,
o arquivo tambem inclui a excecao completa com stack trace e inner exception.

## Retencao

Na inicializacao do logger, arquivos `mpvnet-YYYY-MM-DD.log` com mais de 5 dias
sao apagados automaticamente.

A limpeza atua apenas dentro da pasta de logs e apenas em arquivos que seguem o
padrao `mpvnet-*.log`. Falhas de limpeza ou escrita sao tratadas internamente e
nao devem interromper a interface nem o player.

## Cuidados

Nao registre senha, token, URL privada de playlist, usuario ou dados pessoais.
Se um diagnostico exigir URL, mascare partes sensiveis antes de registrar.
