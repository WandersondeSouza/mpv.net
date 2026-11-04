# Evolução conservadora de atalhos, gestos e OSC

## Objetivo

Avaliar três melhorias futuras do MPV.NET Media Player por etapas:

1. **Baixo risco:** melhorar a documentação dos atalhos de teclado e mouse.
2. **Médio risco:** avaliar controles J/K/L sem perder funções existentes.
3. **Alto risco:** avaliar gestos e alterações no OSC sem regressão nas interações atuais.

Ao executar este prompt, implementar somente a documentação da primeira etapa.
Para J/K/L, gestos e OSC, entregar propostas concretas antes de alterar comportamento.
Uma autorização explícita do usuário para uma dessas propostas permite executar a
respectiva etapa; não pedir novamente uma autorização que já foi concedida.

Salvar este prompt não equivale a executar suas etapas nem a autorizar novos controles.

## Leitura e diagnóstico inicial

Ler e respeitar:

- `AGENTS.md` e `.ai/skills/mpvnet-maintainer.md`;
- `.ai/agents/mpvnet-ui-agent.md` e `.ai/agents/mpvnet-libmpv-agent.md`;
- `README.md`, `docs/manual.md`, `docs/ATALHOS.md` e `docs/guia-operacional.md`;
- `docs/developer/configuration.md`, `docs/developer/windows-ui.md` e
  `docs/developer/mpv-integration.md`;
- `.ai/prompts/player-input-click-pause-fullscreen.md`, como referência de escopo,
  sem tratar suas descrições ou critérios como prova de execução concluída.

Confirmar a branch, o status do Git e o comportamento no código atual. Preservar
alterações locais. Usar uma branch dedicada se for necessária uma nova implementação;
seguir o nome informado pelo usuário ou o prefixo `codex/`.

Analisar inicialmente:

- `src/MpvNet/Utilities/InputHelp.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/InputBindingParser.cs` e `InputBindingSerializer.cs`;
- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/WinForms/MainForm.cs` e os partials relacionados a input,
  cursor, fullscreen, comandos e ciclo de vida existentes na branch;
- `src/MpvNet.Windows/Scripts/osc.lua`;
- cobertura em `src/MpvNet.Tests/Program.cs` e demais testes de input existentes.

Antes de editar, apresentar: entendimento atual, arquivos envolvidos, problema,
mudança mínima proposta, riscos e plano de validação.

Se a validação dos controles de clique simples/duplo clique ainda estiver pendente,
registrar essa dependência. A documentação pode avançar; não acumular novas mudanças
de mouse sobre uma interação que ainda não foi validada.

## Etapa 1 — documentação dos atalhos

Atualizar somente documentos existentes pertinentes, principalmente
`docs/ATALHOS.md` e `docs/manual.md`.

Produzir uma tabela objetiva com ação, binding padrão e observações para:

- Play/Pause, stop, seek, volume, mute e velocidade;
- fullscreen e Esc;
- playlist, faixas de áudio, legendas e repetição;
- botões do mouse, roda, menu de contexto e movimento da janela.

Extrair os padrões do código e confirmar as diferenças entre maiúsculas e
minúsculas. Não apresentar um comportamento planejado como funcionalidade disponível.

Explicar com exemplos mínimos de `input.conf`:

- como personalizar um binding;
- como desativá-lo com `ignore`;
- como bindings explícitos têm prioridade sobre defaults;
- como arquivos com menu completo são tratados;
- como abrir o editor de input e consultar os bindings efetivos.

Não inventar atalhos para preencher a tabela. Não reescrever configurações do usuário.
Evitar repetir tabelas completas em vários documentos; usar links entre eles.

Validar exemplos contra o parser e o fluxo de carregamento atual, conferir links
locais e executar `git diff --check`. Uma alteração exclusivamente documental não
exige novos testes de reprodução nem um build sem justificativa.

## Etapa 2 — proposta de J/K/L

Avaliar o padrão sugerido: J para voltar, K para Play/Pause e L para avançar.
Não assumir que esse padrão define velocidade, seek ou comportamento ao manter
uma tecla pressionada; esclarecer essas escolhas na proposta.

Na análise que originou este prompt, `k` abria o editor de `input.conf`, `l`
controlava o loop A-B e `L` alternava repetição do arquivo. Confirmar esses
bindings novamente, além de `j`, `J`, `K`, modificadores e defaults nativos do mpv.

Entregar uma comparação entre:

- manter os padrões atuais e documentar J/K/L como configuração opcional;
- oferecer uma configuração opcional explicitamente habilitada;
- substituir defaults, somente se o usuário autorizar a perda ou realocação
  das funções conflitantes.

Preferir a alternativa com menor impacto e sem novas dependências. Não criar
um sistema de presets se exemplos de `input.conf` forem suficientes.

Para cada alternativa, apresentar comandos exatos, conflitos, funções afetadas,
tratamento de maiúsculas/minúsculas, repetição de tecla e compatibilidade com
overrides e editor de input. Se houver realocação, verificar conflitos antes de
propor outro atalho.

Play/Pause deve reutilizar o comando oficial existente. Seek e velocidade devem
usar os comandos do mpv, sem implementar reprodução reversa ou estado de pausa
paralelo no frontend. Explicar limitações para streams sem seek e transmissões ao vivo.

Após autorização da proposta, implementar apenas a alternativa escolhida e
testar defaults, overrides, `ignore`, parser/serialização, editor e manutenção
dos atalhos restantes. Atualizar os documentos da etapa 1 conforme o resultado.

## Etapa 3 — proposta de gestos e OSC

OSC é o painel de controles sobre o vídeo, com timeline e botões.
Analisar separadamente cada ideia, por exemplo:

- duplo clique nas laterais do vídeo para seek;
- arraste sobre o vídeo para seek;
- mudanças nos controles ou nas áreas clicáveis do OSC.

Esses exemplos são candidatos de análise, não uma lista de funcionalidades
obrigatórias. Não implementar todos automaticamente.

Mapear quem recebe e consome cada evento: janela Windows, input do mpv, OSC e
scripts do usuário. Inspecionar bindings forçados, áreas de mouse e arbitragem
single/double click existentes. Distinguir heurísticas do frontend das áreas
interativas efetivamente registradas pelo OSC.

Apresentar, por gesto, uma especificação com:

- região de ativação e regiões excluídas;
- comando, quantidade de seek e forma de repetição;
- comportamento com mídia pausada, áudio, buffering e stream sem seek;
- precedência em relação a Play/Pause, fullscreen, drag da janela e timeline;
- habilitação, desabilitação e preservação de configurações existentes;
- cancelamento em troca de mídia, perda de foco e encerramento;
- arquivos necessários, testes e riscos residuais.

Duplo clique lateral disputa o gesto atual de fullscreen. Arraste para seek
disputa o movimento da janela. Não escolher uma precedência silenciosamente:
incluir a decisão de produto na proposta a ser aprovada.

Preferir bindings e mecanismos do mpv/OSC quando resolverem a necessidade.
Só adicionar arbitragem no host após comprovar um conflito real. Não capturar
mouse globalmente, bloquear a UI, usar `Thread.Sleep`, introduzir delays
arbitrários ou modificar `CycleFullscreen` sem evidência de necessidade.

Após autorização, implementar um gesto ou uma alteração do OSC por vez,
com desativação clara e diff limitado. Não modificar núcleo de reprodução,
SMTC, variantes de libmpv, yt-dlp ou framework como parte desta tarefa.

## Validação de mudanças de comportamento autorizadas

Registrar baseline de restore, build e testes antes das alterações. Executar
build e testes finais serialmente, usando o isolamento de dados de testes
adotado pelo projeto. Diferenciar falhas preexistentes de regressões.

Validar manualmente:

- vídeo e áudio locais, URL/stream, playlist, pasta com mídia e drag/drop;
- Play/Pause, clique simples, duplo clique e ausência de pausa durante fullscreen;
- menu de contexto, roda, botões laterais, movimento da janela e resize;
- OSC visível/oculto, timeline, botões e scripts que capturam mouse;
- atalhos antigos, novos e personalizados, incluindo distinção de caixa;
- janela normal/maximizada/fullscreen e retorno ao estado anterior;
- temas claro/escuro, DPI 100%/125%/150% e múltiplos monitores quando disponíveis;
- SMTC, taskbar, cursor auto-hide, troca de mídia e fechamento com ação pendente.

Para gestos, incluir sequências rápidas, clique seguido de arraste, drag iniciado
no OSC e cancelamento de ações atrasadas. Não declarar testes de arbitragem ou
build como prova de funcionamento visual do player.

Quando faltar ambiente, mídia ou acesso para uma validação, registrar exatamente
o que foi executado e o que continua pendente. Não marcar a etapa como aceita
antes de verificar seus critérios relevantes.

## Entrega

Entregar um relatório com:

- etapas implementadas, propostas e dependências pendentes;
- arquivos alterados e comparação dos bindings antes/depois;
- conflitos encontrados e solução escolhida, quando autorizada;
- compatibilidade com `input.conf`, OSC, fullscreen, drag e scripts;
- comandos de validação, resultados e testes manuais efetivamente executados;
- riscos residuais, limitações e critérios de aceite pendentes;
- branch e commit, caso um commit tenha sido solicitado.

Não executar etapas opcionais, criar commits, fazer push, merge ou publicar uma
release apenas por este prompt mencionar essas atividades. Seguir as autorizações
explícitas do usuário para cada uma delas.

---

## Complemento de auditoria comparativa e roteiro de implementação (2026-11-04)

Esta seção não substitui as etapas anteriores. A auditoria foi **estática**, baseada na branch `master`; não foram executados player, build ou testes manuais nesta análise. Reconfirmar a HEAD e a implementação antes de agir. A classificação **implementado no código** não equivale a **validado em ambiente real**.

### Matriz requisito × evidência × ação

| Requisito | Evidência localizada | Situação | Próxima ação |
| --- | --- | --- | --- |
| Clique simples Play/Pause | `MainForm.Input.cs`: `VideoClickArbiter`, timer e invalidação; `InputHelp.cs`: `MBTN_Left`; `PlayerInputTests.cs` | Implementado no código | Apenas regression/smoke Windows; não reimplementar |
| Duplo clique fullscreen | `InputHelp.cs`: `MBTN_Left_DBL`; `MainForm.cs`: encaminhamento de evento, cancelamento da pausa; `MainForm.Fullscreen.cs` | Implementado no código | Validar fullscreen sem mudança em Play/Pause |
| Bindings personalizados e `ignore` | `InputConf.cs`, `InputBindingParser.cs`, `PlayerInputTests.cs` | Implementado no código | Preservar precedência e arquivos completos |
| Consulta/editor de bindings | `InputHelp.cs`: `F2`, `Ctrl+i`, `show-bindings`, `show-keys` | Implementado no código | Melhorar documentação/descoberta, sem duplicar editor |
| Guia consolidado de atalhos | `docs/ATALHOS.md` é um resumo; `docs/manual.md` explica cliques principais | Parcial | Tabela com comandos efetivos, diferenças de caixa e exemplos |
| J/K/L para navegar | `InputHelp.cs`: `k` edita `input.conf`; `l` é A-B loop; `L` alterna loop do arquivo; `Space`, `p` e `P` já fazem Play/Pause | Pendente como opção; conflito demonstrado | Propor perfil opt-in ou exemplos de `input.conf`, sem redefinir padrão |
| Gestos laterais e arrastar para seek | `MainForm.cs` contém arraste da janela; `osc.lua` possui áreas de mouse e timeline | Não foi identificada implementação específica | Especificar conflitos e protótipo desativável; não presumir necessidade |
| OSC permanente e área de vídeo | `osc.lua`: `visibility`, `boxvideo`, margens; `InputHelp.cs`: `Del` | Capacidade base implementada; UX parcial | Coordenar com prompt `user-feedback-persistent-osc-and-keyboard-shortcuts.md` |
| Validação visual Windows | Checklist em `AGENTS.md` e `docs/developer/windows-ui.md` | Não validado nesta auditoria | Smoke real, com evidência e limites do ambiente |

### Sequência recomendada sem duplicação

**Fase A — documentação e diagnóstico (baixo risco).** Inventariar defaults reais, distinguindo `j`/`J`, `k`/`K`, `l`/`L`, modificadores, `Del`, F2 e teclado numérico conforme bindings atuais. Atualizar somente documentos já existentes. Mostrar como testar `ignore`, overrides e editor, sem alterar configurações do usuário. Registrar resultado e verificação de links/diff.

**Fase B — escolha opcional de J/K/L (risco médio).** Preparar comparação com comandos exatos e conflitos: (1) exemplos de `input.conf` (preferido), (2) opção habilitada voluntariamente, (3) substituição global somente se houver autorização específica. Não assumir sem decidir se J/L representam seek incremental ou velocidade. Não implementar reprodução reversa; considerar mídias sem seek, streams ao vivo, repetição de teclas e diferença maiúsculas/minúsculas. Preservar `k` (editor), `l` (loop A-B), `L` (loop de arquivo) por padrão.

**Fase C — gestos e OSC (alto risco, um por vez).** Para cada gesto candidato (duplo clique nas laterais, arraste para seek, alteração de hit areas), elaborar mini-especificação: região, gesto, ação, limiar, exclusões de OSC/timeline, precedência contra fullscreen e movimento da janela, pause/buffering/live, toggle, cancelamento e rollback. Primeiro provar que bindings nativos ou OSC não bastam; nenhum gesto é obrigatório. Implementar somente o gesto aprovado, sem alterar `CycleFullscreen` ou introduzir timers arbitrários, bloqueio da UI ou captura global.

**Fase D — testes e documentação de entrega.** Cobrir `PlayerInputTests.cs` e demais testes de input/parser; executar suíte xUnit v3 segundo `.ai/README.md` e build correspondente quando código mudar, serialmente. Fazer teste manual de mídia local/URL/playlist/pasta, fullscreen, clique simples/duplo, foco, drag, seek, OSC/menu, DPI 100/125/150%, múltiplos monitores, troca de mídia e fechamento. Reportar casos não executados como **não validados**.

### Critérios de aceite adicionais

1. Com defaults, `p`, `P` e `Space` continuam Play/Pause; `k`, `l` e `L` mantêm suas funções. Arquivos personalizados, `ignore`, menus completos e `--input-conf` explícito não são silenciosamente modificados.
2. Ajuda de atalhos corresponde ao parser e aos bindings efetivos; exemplos funcionam com sintaxe real e não divulgam uma funcionalidade proposta como disponível.
3. Qualquer J/K/L opcional é reversível, não cria conflito não informado, não requer dependências novas e não altera comandos de libmpv fora do mecanismo suportado.
4. Clique simples e duplo continuam mutuamente corretos, inclusive em sequências rápidas, clique+arraste, movimento da janela, fullscreen, perda de foco, mudança de mídia e fechamento; OSC e scripts de terceiros conservam prioridade em suas regiões.
5. Em stream sem seek ou mídia pausada, gestos não causam comando incorreto, travamento ou retry infinito; timers e estados pendentes são cancelados quando necessário.
6. Layout e atalhos funcionam nos temas, idiomas, escalas e modos de janela disponíveis; eventuais limitações são documentadas com cenário reproduzível.
7. A entrega inclui matriz final implementado/parcial/pendente/não validado, arquivos alterados, bindings antes/depois, evidência de teste automatizado e manual, riscos e `git diff --check`.

**Fronteira com o outro prompt:** visibilidade persistente, layout e margens de OSC pertencem primariamente ao prompt de feedback; este cuida de documentação de bindings, J/K/L e gestos, evitando propostas de UI duplicadas.

**Autorização:** este complemento e seu commit não autorizam executar as fases B/C, alterar controles existentes nem publicar. Obedecer à solicitação explícita de implementação e aos limites do `AGENTS.md`.
