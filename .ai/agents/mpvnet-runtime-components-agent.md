# Agente: Componentes auxiliares e cache de runtime do MPV.NET

## Missão

Manter o ciclo dos componentes auxiliares com armazenamento seguro, validação de
integridade, atualização recuperável, diagnóstico e compatibilidade entre
instalação comum, modo portátil e MSIX/Microsoft Store.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md` e `docs/manual.md`;
3. `docs/guia-operacional.md` e `docs/logging.md`;
4. `docs/developer/configuration.md`;
5. `docs/developer/build-release.md` quando a mudança envolver dependências de
   build, payload ou Store;
6. `docs/developer/mpv-integration.md` quando também tocar cache de mídia de
   rede.

## Arquivos críticos

- `src/MpvNet/RuntimeComponents.cs`;
- `src/MpvNet/Infrastructure/RuntimeComponents/`;
- `src/MpvNet/Infrastructure/Folder.cs`;
- `src/MpvNet/Integration/Mpv/Player.NetworkCache.cs`, quando aplicável;
- `src/MpvNet.Tests/GitHubReleaseClientTests.cs`;
- `src/MpvNet.Tests/RuntimeComponentStorageTests.cs`;
- `src/MpvNet.Tests/NetworkCachePolicyTests.cs`, quando aplicável.

## Baseline a confirmar no código

- `yt-dlp`, Deno e o bundle FFmpeg são baixados em runtime; `ffmpeg`,
  `ffplay` e `ffprobe` são promovidos como conjunto.
- `libmpv`, `MediaInfo.dll` e `mpvnet.com` pertencem ao fluxo de dependências e
  payload do build. `mpvnet.com` permanece empacotado por compatibilidade, mas
  não é baixado nem iniciado pelo player.
- `AppPaths` deriva a raiz comum de dados; `MPVNET_LOCAL_APPDATA` tem
  precedência e o MSIX usa `Packages\<PackageFamilyName>\LocalCache\Local`.
- O serviço centraliza o intervalo de atualização e preserva uma geração válida
  em caso de falha. Verifique o valor e a semântica atuais em código e
  documentação antes de propor mudanças.
- O mecanismo atual valida SHA-256 publicado, staging e promoção; transfers de
  runtime têm suporte a retomada. Reconfirme limites, tentativas e limpeza nos
  documentos e testes atuais.
- A política de cache de reprodução é uma área distinta: consulte
  `NetworkCachePolicy` e preserve opções explícitas do usuário.

## Regras

- Não criar outro resolvedor ou gerenciador concorrente.
- Não presumir caminhos a partir do diretório de trabalho nem gravar no pacote
  MSIX instalado.
- Preservar o cache válido anterior durante falha, cancelamento ou promoção.
- Validar nomes, arquitetura, integridade, conteúdo ZIP e limites de extração
  antes de ativar arquivos baixados.
- Não depender de Internet real nos testes automatizados.
- Registrar evidência separada para teste local, uso real de Store e política
  externa de distribuição.
- Não fazer commit, push ou publicação sem solicitação explícita.

## Validação

- Cobrir download, retomada, hash, cache, staging, promoção, falha e
  concorrência nos testes existentes.
- Executar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`
  quando o comportamento for alterado.
- Para alterações de paths ou payload, revisar `MPVNET_LOCAL_APPDATA`, modo
  portátil e resolução de dados no MSIX; o build local não prova submissão ou
  certificação Store.
