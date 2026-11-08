# Agente: Configuração e atalhos do MPV.NET Media Player

## Missão

Analisar e manter o sistema de configuração, modo portátil, `mpv.conf`, `mpvnet.conf`, `input.conf`, temas e hotkeys globais sem quebrar arquivos existentes de usuários.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/CONFIGURACAO.md`;
5. `docs/ATALHOS.md`;
6. `docs/guia-operacional.md`;
7. `docs/developer/configuration.md`;
8. `docs/developer/architecture.md` quando a mudança envolver fluxo amplo.

## Arquivos críticos

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/Integration/Mpv/Player.Initialization.cs`;
- `src/MpvNet/Integration/Mpv/Player.MediaLoading.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/NetworkCacheConfiguration.cs`;
- `src/MpvNet.Windows/WPF/ConfWindow.xaml.cs` e `Resources/editor_conf.txt`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/Settings.cs`;
- `src/MpvNet/Infrastructure/Folder.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

## Regras

- Preservar a seleção central em `AppPaths`: `--config-dir` explícito, `MPVNET_HOME` válido, `portable_config` existente em processo sem pacote e, por fim, `AppPaths.DefaultConfig` sob `AppPaths.LocalAppData`. Frontend e mpv devem usar a mesma raiz antes de carregar configuração.
- Não presumir o caminho físico padrão do MSIX: `AppPaths` usa `Packages\<PackageFamilyName>\LocalCache\Local`; `MPVNET_LOCAL_APPDATA` substitui a raiz de dados nos dois modos.
- Não normalizar ou reescrever arquivo do usuário sem necessidade.
- Criar backup antes de migração que altere arquivo do usuário.
- Cache de streaming tem somente `automatic-network-cache`. Ler os perfis antigos como migração em memória; preservar `off`/toggle `no`, aliases de CLI e precedência. A primeira gravação de uma alteração no editor remove o legado e preserva `mpvnet.conf.network-cache.backup`; não regravar no startup ou ao abrir/fechar sem editar.
- Para migração e interação entre opções, consultar [configuração](../../docs/developer/configuration.md#mpvnetconf) e [cache automático](../../docs/developer/mpv-integration.md#cache-automático-por-item). Não confundir desligar a política com forçar `cache=no` no mpv.
- Validar atalhos duplicados sem quebrar compatibilidade com sintaxe do mpv.
- Documentar opção ou fallback consolidado em documento existente quando possível.
- Evitar criar documentação técnica redundante.
- Quando uma migração afetar mais de um arquivo, descrever a sequência exata e o motivo.

## Testes manuais esperados

- Executar com `portable_config` ao lado do `mpvnet.exe`.
- Executar sem `portable_config`.
- Executar com `MPVNET_HOME`.
- Testar `input.conf` padrão e customizado.
- Testar menu de contexto quando `input.conf` contém sintaxe de menu.
- Verificar e ampliar os testes da área em `src/MpvNet.Tests/` (incluindo `Program.cs` quando aplicável) quando tocar parser, paths, playlist, título, configuração, seleção de idioma ou MediaInfo; depois rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.



