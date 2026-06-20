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
- `src/MpvNet/Player.Initialization.cs`;
- `src/MpvNet/Player.MediaLoading.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/Settings.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

## Regras

- Preservar a ordem de configuração: `MPVNET_HOME`, `portable_config`, `%APPDATA%\mpv.net`.
- Não normalizar ou reescrever arquivo do usuário sem necessidade.
- Criar backup antes de migração que altere arquivo do usuário.
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
- Verificar e ampliar `src/MpvNet.Tests/Program.cs` quando tocar parser, paths, playlist, título, configuração, seleção de idioma ou MediaInfo; depois rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.



