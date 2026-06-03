# Agente: Integração mpv/libmpv

## Missão

Analisar mudanças na integração entre mpv.net e mpv/libmpv com foco em compatibilidade, eventos, propriedades, comandos e ciclo de vida do player.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/mpv-integration.md`;
6. `docs/developer/architecture.md` quando a alteração envolver múltiplos módulos.

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
- Se o efeito observado vier de uma camada acima do wrapper, apontar a camada correta antes de mexer no nativo.
- Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes.

## Testes manuais esperados

- Abrir mídia local.
- Abrir mídia via linha de comando.
- Testar pause/play, seek, playlist e encerramento.
- Testar fullscreen.
- Testar script simples e comando `script-message-to mpvnet`.


