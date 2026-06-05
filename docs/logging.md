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

## Gerar ZIP e instalador para teste

Execute estes comandos na raiz do repositorio para gerar artefatos locais com
logs habilitados.

### Gerar apenas o ZIP portatil com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saidas esperadas:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64.zip
```

Para testar, extraia o ZIP e execute `mpvnet.exe` dentro da pasta extraida.

### Gerar apenas o instalador executavel com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saidas esperadas:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-setup-x64.exe
```

Para testar, execute o instalador `setup-x64.exe`, abra o aplicativo instalado e
reproduza os cenarios que deseja diagnosticar.

### Gerar ZIP e instalador juntos com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -EnableFileLogging
```

Esse comando gera os artefatos locais sem publicar no GitHub.

Depois de abrir o aplicativo gerado, verifique o log em:

```powershell
explorer "$env:LOCALAPPDATA\mpv.net\Logs"
```

O arquivo do dia deve seguir o formato `mpvnet-YYYY-MM-DD.log`.

## Onde os logs ficam

Quando habilitado, o aplicativo grava em:

```text
%LOCALAPPDATA%\mpv.net\Logs
```

Essa pasta compartilha a mesma raiz usada pelo cache temporario do mpv:

```text
%LOCALAPPDATA%\mpv.net\Cache
```

Na inicializacao, falhas ao limpar arquivos antigos em
`%LOCALAPPDATA%\mpv.net\Cache` ou `%LOCALAPPDATA%\mpv.net\Temp` tambem entram
nesse log apenas quando o build foi gerado com logging em arquivo habilitado.
Essas falhas nao bloqueiam a abertura do player.

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

## Diagnostico de abertura por parametro

Builds com logs habilitados registram detalhes adicionais do fluxo de abertura:

- argumentos recebidos pela linha de comando;
- opcoes aplicadas antes e depois de `mpv_initialize`;
- arquivos, playlists ou URLs classificados como entrada de midia;
- comandos `loadfile` e `loadlist` enviados ao mpv;
- valor final de `idle` e se o frontend deve sair ao fim da reproducao;
- eventos `start-file`, `file-loaded`, `end-file` e `shutdown` emitidos pelo mpv.

URLs com query string ou fragmento sao mascaradas no log para evitar expor
tokens de playlists privadas.

## Retencao

Na inicializacao do logger, arquivos `mpvnet-YYYY-MM-DD.log` com mais de 5 dias
sao apagados automaticamente.

A limpeza atua apenas dentro da pasta de logs e apenas em arquivos que seguem o
padrao `mpvnet-*.log`. Falhas de limpeza ou escrita sao tratadas internamente e
nao devem interromper a interface nem o player.

## Cuidados

Nao registre senha, token, URL privada de playlist, usuario ou dados pessoais.
Se um diagnostico exigir URL, mascare partes sensiveis antes de registrar.
