# Prompt — Mudança na integração mpv/libmpv

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/mpv-integration-ptbr.md`;
6. `docs/developer/libmpv-wrapper-ptbr.md`;
7. `docs/developer/event-flow-ptbr.md`;
8. `docs/developer/commands-ptbr.md`.

Objetivo:

```text
[DESCREVA A MUDANÇA OU BUG NA INTEGRAÇÃO COM MPV/LIBMPV]
```

Antes de alterar qualquer arquivo:

- confirme o fluxo atual em `Player`, `MpvClient` e `LibMpv`;
- identifique comandos, propriedades e eventos afetados;
- avalie impacto em scripts, `input.conf`, fullscreen e encerramento;
- compare com comportamento esperado do mpv quando aplicável.

Regras:

- tratar como alto risco;
- não renomear comando, opção ou propriedade sem migração;
- não bloquear loop de eventos;
- preferir correção pequena e testável;
- atualizar documentação técnica quando comportamento mudar.

Plano de teste mínimo:

- abrir mídia local;
- testar pause/play/seek;
- testar fullscreen;
- testar playlist;
- testar script/comando relacionado;
- encerrar o aplicativo.
