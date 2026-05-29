# Prompt — Mudança na integração mpv/libmpv

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/mpv-integration.md`;
6. `docs/developer/mpv-integration.md`;
7. `docs/developer/mpv-integration.md`;
8. `docs/developer/mpv-integration.md`.

Se a alteração cruzar mais de um módulo, consulte também `docs/developer/architecture.md` e `docs/developer/architecture.md`.

Objetivo:

```text
[DESCREVA A MUDANÇA OU BUG NA INTEGRAÇÃO COM MPV/LIBMPV]
```

Antes de alterar qualquer arquivo:

- confirme o fluxo atual em `Player`, `MpvClient` e `LibMpv`;
- identifique comandos, propriedades e eventos afetados;
- avalie impacto em scripts, `input.conf`, fullscreen e encerramento;
- compare com comportamento esperado do mpv quando aplicável.

Se houver dúvida sobre o ponto de entrada do fluxo, pare e localize primeiro a chamada real no código em vez de assumir o caminho.

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


