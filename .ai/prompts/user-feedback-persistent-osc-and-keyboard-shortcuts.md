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

---

## Auditoria comparativa da branch master (2026-11-04)

Esta seção acrescenta a auditoria de experiência do usuário ao prompt original. É uma **análise estática**: a existência de código, testes e documentação não comprova execução visual no Windows nem aprovação de todos os cenários. Antes de implementar, repetir a leitura na HEAD atual e reclassificar cada requisito como **implementado no código**, **parcial**, **pendente**, **não validado** ou **fora de escopo**, anexando arquivo/linha e evidência verificável. Não executar trabalhos já concluídos.

### Matriz de evidências e lacunas

| Requisito | Evidência encontrada em 2026-11-04 | Situação da auditoria | Ação incremental |
| --- | --- | --- | --- |
| OSC automático, sempre visível ou oculto | `src/MpvNet.Windows/Scripts/osc.lua`: `visibility=auto`, `visibility_modes=never_auto_always`, `osc-visibility`; `src/MpvNet/Utilities/InputHelp.cs`: `Del` | Implementado no código; persistência/UX não validada | Testar preferências e descoberta antes de criar UI |
| Controles permanentes sem cobrir o vídeo | `osc.lua`: `boxvideo`, `dynamic_margins`, `sub_margins`, `osd_margins` (defaults não habilitam `boxvideo`) | Parcial / não validado visualmente | Provar ajuste real em janela, maximizado, fullscreen e com legendas |
| Atalhos padrão e substituições explícitas | `InputHelp.cs`, `InputConf.cs`, `InputBindingParser.cs`; `src/MpvNet.Tests/PlayerInputTests.cs` | Implementado no código | Não substituir defaults nem reescrever `input.conf` |
| Consulta de atalhos ativos | `InputHelp.cs`: `show-bindings`, `show-keys`, seletor de bindings e editor | Implementado no código; descoberta parcial | Documentar acesso e avaliar ajuda contextual mínima |
| Teclado, foco e menus | `MainForm.cs`: `WndProc` encaminha mensagens WM_KEY* ao mpv e dá prioridade a Esc/menu/fullscreen | Parcial / relato externo não reproduzido | Produzir diagnóstico com foco e configurações padrão/personalizadas |
| Clique simples/duplo clique, OSC e arraste | `MainForm.Input.cs`, `MainForm.cs`, `PlayerInputTests.cs` | Implementado no código; teste visual ainda necessário | Proteger comportamento durante qualquer alteração de OSC |

### Plano de execução priorizado

1. **Baseline:** capturar branch/HEAD/status, ler documentação, opções efetivas do OSC e testes; coletar passos de reprodução sem exigir informações desnecessárias do usuário. Não alterar código ainda.
2. **Descoberta e documentação:** verificar se `Del`, `F2`, `Ctrl+i`, comandos de listagem de bindings e configurações já resolvem o pedido; atualizar preferencialmente `docs/manual.md` e `docs/ATALHOS.md` com caminhos e exemplos verificados. Distinguir a barra OSC sobreposta de uma barra nativa reservando espaço para o vídeo.
3. **Experiência do OSC:** testar `visibility=always` com `boxvideo` e margens, inclusive legendas e conteúdo anamórfico. Se houver lacuna real, propor e implementar a menor mudança possível para oferecer **Automático / Sempre visível / Oculto** na interface ou configuração atual, respeitando `script-opts/osc.conf`, scripts e opções explícitas. Não criar estado paralelo ao OSC.
4. **Diagnóstico de atalhos:** mapear o caminho Win32 → mpv, perda/recuperação de foco, menus e `input.conf`; investigar `input-test`, `input-keylist`, `show-bindings` e `show-keys`. Corrigir apenas defeito demonstrado, sem mudar teclas padrão.
5. **Regressões e entrega:** executar testes relevantes, build quando houver código alterado e smoke manual Windows; entregar matriz antes/depois com evidências, limites e pendências por ambiente.

### Critérios de aceite verificáveis

- A opção automática continua padrão; escolhas explícitas do usuário têm precedência, persistem após reinicialização quando configuradas e são reversíveis sem editar arquivos do usuário indevidamente.
- No modo permanente, controles e legendas não se sobrepõem de maneira indevida; verificar mídia 16:9, 4:3, ultrawide, vídeo vertical, áudio-only, janela normal/maximizada/fullscreen, temas claro/escuro, DPI 100/125/150% e segundo monitor quando disponível.
- OSC e scripts ainda recebem mouse/hover corretamente; clique simples pausa uma única vez, duplo clique alterna fullscreen sem pausa, arrastar janela e menu não provocam pausas acidentais.
- Com perfil padrão e `input.conf` personalizado, documentar quais atalhos funcionam e por quê. Testar foco ao fechar menu, ao alternar janela, com `Esc`, `ignore`, bindings duplicados e `--input-conf` explícito.
- Testes relevantes em `src/MpvNet.Tests/` passam usando o comando recomendado em `.ai/README.md`; registrar logs de execução e checklist visual sem confundir teste unitário com comprovação em Windows.
- Nenhum novo framework de UI, timer arbitrário, interceptação global de teclado/mouse, mudança no libmpv ou migração automática de `input.conf` sem necessidade comprovada e escopo autorizado.
- A entrega identifica arquivos alterados, impacto em desempenho, acessibilidade, localização (idiomas existentes), riscos, `git diff --check` e testes executados versus pendentes.

**Limite de autorização:** atualizar este prompt e fazer seu commit não autoriza a implementação de comportamento, alteração de defaults ou release. Implementar funcionalidades apenas quando solicitado explicitamente.
