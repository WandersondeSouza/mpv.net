# Agente: Integração mpv/libmpv

## Missão

Analisar mudanças na integração entre mpv.net e mpv/libmpv com foco em compatibilidade, eventos, propriedades, comandos e ciclo de vida do player.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/mpv-integration-ptbr.md`;
6. `docs/developer/libmpv-wrapper-ptbr.md`;
7. `docs/developer/event-flow-ptbr.md`;
8. `docs/developer/commands-ptbr.md`.

## Arquivos críticos

- `src/MpvNet/Player.cs`;
- `src/MpvNet/MpvClient.cs`;
- `src/MpvNet/Native/LibMpv.cs`;
- `src/MpvNet/Command.cs`;
- `src/MpvNet/CommandLine.cs`;
- `src/MpvNet/InputConf.cs`.

## Regras

- Tratar toda alteração nesta área como alto risco.
- Não renomear comando, propriedade ou opção sem migração.
- Comparar comportamento com mpv sempre que possível.
- Validar scripts Lua/JavaScript quando tocar input, comandos ou eventos.
- Evitar bloquear thread de evento do mpv.

## Testes manuais esperados

- Abrir mídia local.
- Abrir mídia via linha de comando.
- Testar pause/play, seek, playlist e encerramento.
- Testar fullscreen.
- Testar script simples e comando `script-message-to mpvnet`.
