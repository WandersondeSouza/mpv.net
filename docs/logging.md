# Logs de diagnóstico

O MPV.NET Media Player possui um logger interno simples para diagnóstico de suporte.
Ele grava erros em arquivo em qualquer build e habilita logs detalhados apenas
quando o build e gerado com logging habilitado.

Resumo rápido:

- `Error` sempre vai para arquivo.
- `Debug` só vai para arquivo em build de diagnóstico.
- Logs diarios sao retidos por 3 dias.
- Limpeza de `Cache` e `Temp` tambem usa 3 dias.

## Estado padrão

Logs detalhados em arquivo ficam desabilitados por padrão.
Erros continuam sendo gravados em arquivo.

Use esse padrão para releases publicas:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=false
```

O mesmo padrão e usado pelos scripts de release quando nenhum parametro extra e informado.

## Como habilitar

Para gerar uma versao de diagnóstico com logs detalhados em arquivo:

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
`enable_file_logging=true` apenas para pacotes de diagnóstico/suporte.

## Gerar ZIP e instalador para teste

Execute estes comandos na raiz do repositório para gerar artefatos locais com
logs habilitados.

### Gerar apenas o ZIP portátil com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saidas esperadas:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64.zip
```

Para testar, extraia o ZIP e execute `mpvnet.exe` dentro da pasta extraida.

### Gerar apenas o instalador executável com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saidas esperadas:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-setup-x64.exe
```

Para testar, execute o instalador `setup-x64.exe`, abra o aplicativo instalado e
reproduza os cenários que deseja diagnosticar.

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

Quando existe escrita em arquivo, o aplicativo grava em:

```text
%LOCALAPPDATA%\mpv.net\Logs
```

Essa pasta compartilha a mesma raiz usada pelo cache temporário do mpv:

```text
%LOCALAPPDATA%\mpv.net\Cache
```

Na inicialização, falhas ao limpar arquivos antigos em
`%LOCALAPPDATA%\mpv.net\Cache` ou `%LOCALAPPDATA%\mpv.net\Temp` tambem entram
nesse log. Em builds de diagnóstico, essas falhas aparecem junto com os logs
`Debug`; nos demais builds, apenas erros sao persistidos.
Essas falhas nao bloqueiam a abertura do player.

O arquivo diario usa este padrão:

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

Na inicialização do logger, arquivos `mpvnet-YYYY-MM-DD.log` com mais de 3 dias
sao apagados automaticamente.

A limpeza atua apenas dentro da pasta de logs e apenas em arquivos que seguem o
padrão `mpvnet-*.log`. Falhas de limpeza ou escrita sao tratadas internamente e
nao devem interromper a interface nem o player.

## Cuidados

Nao registre senha, token, URL privada de playlist, usuario ou dados pessóais.
Se um diagnóstico exigir URL, mascare partes sensiveis antes de registrar.

