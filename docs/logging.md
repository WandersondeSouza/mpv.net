# Logs de diagnóstico

O MPV.NET Media Player possui um logger interno simples para diagnóstico de suporte.
Ele grava erros em arquivo em qualquer build e habilita logs detalhados apenas
quando o build e gerado com logging habilitado.

Resumo rápido:

- `Error` sempre vai para arquivo.
- `Debug` só vai para arquivo em build de diagnóstico.
- Logs diarios sao retidos por 3 dias.
- Limpeza de `Cache` e `Temp` tambem usa 3 dias.

Falhas reconhecidas de transporte curl e de decodificação de vídeo durante
streaming também geram `Streaming runtime failure` em `Error`, mesmo com
`debug-mode=no` e sem um evento final de erro. O resumo é limitado a uma entrada
por categoria e item da playlist; os detalhes repetidos ficam no diagnóstico.
O registro não reinicia a mídia nem troca a playlist. Erros de decodificação
não são classificados como falhas transitórias de rede.

## Estado padrão

Logs detalhados em arquivo ficam desabilitados por padrão.
Erros continuam sendo gravados em arquivo.

O cancelamento de um download de componente pelo encerramento do aplicativo é
esperado e aparece somente em `Debug`. Timeout e falhas reais de download
continuam em `Error`; o timeout informa o limite e os bytes recebidos.
Downloads retomáveis preservam parciais no fechamento, em timeout ou falha
transitória. O limite é de duas horas por transferência, com detecção de
60 segundos sem progresso. Em builds de diagnóstico, o bootstrap registra
bytes retomados, progresso a cada cinco segundos, velocidade média, duração
das fases e reaproveitamento de uma release com SHA-256 inalterado.

Os testes xUnit isolam automaticamente `MPVNET_LOCAL_APPDATA` em
`test-runtime` dentro da pasta do executável de testes quando não existe um
override explícito. Assim, os testes de XML/JSON inválidos mantêm seus erros
no log de testes, sem misturá-los ao log do player instalado. Um
`MPVNET_LOCAL_APPDATA` fornecido pelo desenvolvedor ou CI continua sendo usado.

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

Essa compilação também inicia com `debug-mode=yes`, salvo quando
`mpvnet.conf` define explicitamente `debug-mode=no`. Isso ativa os detalhes de
cache de rede, mudanças de pausa, mensagens `WARN`/`ERROR`/`FATAL` do libmpv e
o arquivo de `Trace` `MpvNet-debug.log` na pasta de configuração. Esse arquivo
de `Trace` é recriado na inicialização; os logs diários `mpvnet-YYYY-MM-DD.log`
continuam separados.

Nessas builds, `debug-mode=yes` também habilita `curl=trace`, `demux=debug`,
`lavf=debug` e `ffmpeg/demuxer=debug`. Uma configuração explícita de `msg-level`
no `mpv.conf` ou na linha de comando tem precedência. Os eventos selecionados
vão para o log diário sanitizado, incluindo status HTTP, ranges e tentativas de
transferência; o trace por quadro/pacote dos demais módulos não é habilitado.
Cabeçalhos sem interesse para transporte, credenciais e queries são ocultados.
URLs que trazem assinatura, token ou endereço do cliente em segmentos do
caminho (como `/signature/`, `/lsig/` e `/ip/`) têm o caminho ocultado antes
do limite de tamanho. Isso também vale para o alvo relativo de uma requisição
HTTP no trace do curl.
A saída do yt-dlp é decodificada quando recebida como string JSON e gravada
por linha; mensagens longas são divididas após a sanitização, preservando o
restante do diagnóstico. Dumps de argumentos de configuração são ocultados
por poderem conter senhas, cookies ou tokens.

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

URLs com usuário, senha, query string ou fragmento sao mascaradas no log para
evitar expor credenciais e tokens de playlists privadas. Diagnósticos de cache
registram o tipo de mídia e o perfil escolhido, nunca a query original.

Com `debug-mode=yes` em uma build com logging de diagnóstico, `Network cache
state` registra as opções efetivas no hook `on_load` e no `file-loaded`.
`buffering-start` e `buffering-end` registram pausas por cache e a duração
da espera e posição, com buffer e taxa de entrada estimados. `Network cache
stream durations` informa os segundos disponíveis para áudio/vídeo quando o
mpv expõe esses valores em `ts-per-stream`. Esses valores descrevem
o demuxer principal e não garantem cobertura conjunta das faixas separadas.

Em builds de diagnóstico, o app acrescenta `verbose=` a
`ytdl-raw-options`. Assim, o `yt-dlp` selecionado pelo hook do mpv grava a saída
de `stderr` no log diário depois da extração, inclusive quando ela falha.
Mensagens passam por remoção de URLs assinadas e valores de segredo. O JSON de
`stdout` não é registrado porque contém URLs temporárias dos fluxos. Para
reproduzir esse modo manualmente, o yt-dlp documenta `--verbose` como a opção
de informações de depuração e o mpv encaminha `ytdl-raw-options` ao processo
filho.

## Retencao

Na inicialização do logger, arquivos `mpvnet-YYYY-MM-DD.log` com mais de 3 dias
sao apagados automaticamente.

A limpeza atua apenas dentro da pasta de logs e apenas em arquivos que seguem o
padrão `mpvnet-*.log`. Falhas de limpeza ou escrita sao tratadas internamente e
nao devem interromper a interface nem o player.

## Cuidados

Nao registre senha, token, URL privada de playlist, usuario ou dados pessóais.
Se um diagnóstico exigir URL, mascare partes sensiveis antes de registrar.

