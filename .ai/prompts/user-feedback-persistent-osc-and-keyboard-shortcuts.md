# Investigação de feedback: controles persistentes e atalhos de teclado

## Objetivo

Analisar o feedback abaixo e propor melhorias pequenas, compatíveis com
mpv/libmpv e baseadas no código atual da branch.

```text
So, I switched to this after my windows gave up on VLC and this app is absolutely crazy. Idk how much features it has compared to other media players. But this does everything that I need from a player, like save progress, plays multiple formats. And the best one, the clean UI. I just love how it instantly hides UI as soon as you move your cursor out of the range, idk why this basic thing is missed by so many video players. Idk if it is already possible but it would be better to make the UI permanently visible and fit the video according to that. (Like they have in VLC) and this would be perfect. And can you pls check if the keyboard shortcuts are working, they aren't working for me.
```

## Escopo de investigação

Investigue separadamente:

### 1. Controles sempre visíveis e ajuste do vídeo

- Confirme o comportamento atual do OSC distribuído e as opções disponíveis
  para visibilidade, autocultação e margens do vídeo.
- Verifique se já é possível deixar os controles sempre visíveis por atalho ou
  configuração, se a escolha persiste ao reiniciar e se o vídeo é ajustado sem
  ficar encoberto.
- Avalie se basta documentar a configuração existente ou se vale oferecer uma
  preferência clara na interface.
- Preserve o modo automático atual como comportamento padrão, a menos que haja
  justificativa baseada em evidências para mudá-lo.
- Considere janela normal, maximizada e fullscreen, tema claro/escuro, DPI,
  menu de contexto, mouse, legendas e overlays.
- Não confunda controles OSC sobrepostos ao vídeo com uma barra de ferramentas
  nativa da janela; esclareça qual interpretação corresponde ao pedido.

### 2. Atalhos de teclado

- Não presuma que os atalhos estejam quebrados. Mapeie o caminho de entrada de
  teclado até o mpv e identifique como foco, menus/janelas auxiliares,
  `input.conf` personalizado e bindings explícitos podem afetar o resultado.
- Verifique como listar os atalhos ativos e as teclas reconhecidas pelo
  aplicativo.
- Identifique quais evidências faltam para reproduzir o relato, incluindo quais
  teclas foram pressionadas, se a janela principal estava focada e se o problema
  ocorre com a configuração padrão.
- Se possível, proponha um roteiro reproduzível de diagnóstico e uma correção
  apenas quando o código ou a reprodução apontarem uma falha concreta.

## Leitura e regras

Antes de recomendar alterações, leia `AGENTS.md`, `README.md`, `docs/manual.md`,
`docs/guia-operacional.md`, `.ai/skills/mpvnet-maintainer.md`,
`docs/developer/windows-ui.md`, `docs/developer/mpv-integration.md`,
`docs/developer/configuration.md`, `docs/ATALHOS.md` e os arquivos de código
relacionados.

Não edite arquivos nem altere atalhos padrão durante a investigação. Separe
fatos observados no código de hipóteses ainda não reproduzidas.

## Entrega

Entregue:

- resumo do comportamento atual com referências a arquivos e trechos relevantes;
- classificação de cada sugestão como configuração já disponível, melhoria de
  descoberta/documentação ou mudança de código;
- ganhos esperados, custos e riscos;
- uma proposta pequena e priorizada;
- critérios de aceite e plano de validação;
- questões que precisam de resposta do usuário, sem bloquear as análises que já
  podem ser feitas.

Não execute mudanças opcionais sem autorização explícita. Se houver autorização
para implementar, confirme primeiro os detalhes que alterem atalhos padrão ou
comportamento visível e preserve configurações existentes.
