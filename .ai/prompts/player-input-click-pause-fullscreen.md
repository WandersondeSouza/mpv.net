# Implementação conservadora de interações modernas de mouse e teclado no MPV.NET Media Player

## Objetivo

Implementar, de forma **mínima, isolada e sem regressão**, comportamentos comuns de players modernos no MPV.NET Media Player, preservando a estabilidade conquistada após meses de testes e correções.

A prioridade absoluta desta tarefa é **não alterar o núcleo de reprodução, não introduzir regressões em fullscreen, OSC, SMTC, input.conf, drag da janela, cursor ou integração com mpv/libmpv**.

Antes de modificar qualquer código, leia e respeite:

- `AGENTS.md`
- `.ai/skills/mpvnet-maintainer.md`
- `.ai/agents/mpvnet-ui-agent.md`
- `.ai/agents/mpvnet-libmpv-agent.md`
- `docs/manual.md`
- `docs/ATALHOS.md`
- `docs/developer/mpv-integration.md`
- `docs/developer/windows-ui.md`
- `docs/developer/configuration.md`

Também analise o estado atual da branch antes de concluir que este prompt representa exatamente o código atual.

---

## Comportamento desejado

### Mouse

Quando houver mídia carregada e o clique ocorrer na área de vídeo:

1. **Clique simples com botão esquerdo**
   - se estiver reproduzindo, pausar;
   - se estiver pausado, retomar a reprodução;
   - usar o mesmo fluxo já existente de Play/Pause sempre que possível;
   - não criar um segundo mecanismo concorrente de controle de pausa.

2. **Duplo clique com botão esquerdo**
   - se estiver em janela normal, entrar em tela cheia;
   - se estiver em tela cheia, voltar ao modo anterior;
   - preservar o comportamento atual de fullscreen;
   - não reimplementar `CycleFullscreen` se o fluxo atual já funciona corretamente.

3. **Clique direito**
   - continuar abrindo o menu de contexto exatamente como hoje.

4. **Botão do meio**
   - preservar o comportamento atual, salvo se a análise demonstrar conflito real.

5. **Roda do mouse**
   - preservar os bindings atuais de volume.

6. **Botões laterais**
   - preservar navegação atual de playlist.

### Teclado

1. **P / p**
   - alternar Play/Pause;
   - se reproduzindo, pausar;
   - se pausado, continuar.

2. **Space**
   - continuar funcionando como Play/Pause.

3. Preservar todos os demais atalhos existentes, exceto quando houver conflito inevitável documentado.

---

## Estado atual já identificado

No estado analisado do repositório:

- `MBTN_Left` está configurado como `ignore`;
- `MBTN_Left_DBL` já está configurado como `cycle fullscreen`;
- `Space` já usa `script-message-to mpvnet play-pause`;
- teclas multimídia Play/Pause já usam o fluxo de Play/Pause;
- `p` atualmente está associado a `show-progress`;
- `Enter` e `f` possuem bindings relacionados a fullscreen;
- `MainForm.Fullscreen.cs` contém lógica importante de fullscreen, restauração de janela, borda e Media Transport;
- `MainForm.cs` também contém lógica de drag de janela, cursor, menu, lifecycle e input de baixo nível.

Não assuma que estes pontos permanecem iguais. Confirme-os na branch antes de alterar.

---

## Regra principal de arquitetura

### Primeira opção: reutilizar o sistema de bindings do mpv

A implementação deve **preferencialmente ocorrer no sistema de bindings já existente**, especialmente em:

- `src/MpvNet/Utilities/InputHelp.cs`
- `src/MpvNet/Configuration/InputConf.cs`

Evite adicionar handlers WinForms/WPF específicos se o próprio input do mpv resolver corretamente a necessidade.

Use o mesmo comando de Play/Pause já consolidado pelo projeto:

`script-message-to mpvnet play-pause`

ou outro caminho já adotado oficialmente pelo projeto caso a análise da branch mostre que ele mudou.

Não implementar uma segunda lógica de estado baseada em variável própria de pausa se a propriedade real do mpv já é a fonte de verdade.

---

## Ponto crítico: clique simples versus duplo clique

Este é o maior risco da tarefa.

Não altere simplesmente:

`MBTN_Left ignore`

para Play/Pause e considere a tarefa concluída.

Primeiro valide experimentalmente como a versão atual do mpv/libmpv entrega:

- `MBTN_Left`
- `MBTN_Left_DBL`

Verifique se um duplo clique também dispara um clique simples antes do evento de duplo clique.

### Resultado esperado

Um duplo clique deve produzir apenas:

`toggle fullscreen`

Ele **não pode**:

- pausar antes de entrar em fullscreen;
- dar play antes de sair de fullscreen;
- alternar Play/Pause duas vezes;
- gerar flicker;
- criar atraso perceptível desnecessário;
- interferir no OSC.

### Caso o binding puro funcione

Se `MBTN_Left` + `MBTN_Left_DBL` funcionarem de forma independente no mpv atual, use somente bindings.

Essa é a solução preferida.

### Caso exista conflito real

Somente se testes comprovarem que o primeiro clique simples é executado durante o duplo clique, implementar uma camada mínima de arbitragem.

Requisitos dessa arbitragem:

- usar o intervalo oficial de double-click do Windows, por exemplo `SystemInformation.DoubleClickTime`, em vez de um número mágico;
- após o primeiro clique, aguardar apenas o intervalo necessário para distinguir single/double click;
- se surgir o segundo clique válido, cancelar a ação pendente de Play/Pause e executar somente fullscreen;
- se não surgir segundo clique, executar Play/Pause;
- não usar `Thread.Sleep`;
- não bloquear UI thread;
- usar mecanismo cancelável;
- cancelar corretamente timers/tasks no Dispose;
- não gerar race condition ao fechar o player;
- não interferir com drag da janela;
- não interferir com clique no OSC;
- não disparar ao clicar em controles, menus ou elementos interativos do player.

Antes de criar essa camada, documentar claramente por que os bindings do mpv não foram suficientes.

---

## Área de clique

O clique simples Play/Pause deve valer para a **área de vídeo**, não indiscriminadamente para toda a janela.

Validar especialmente:

- OSC visível;
- timeline;
- botões do OSC;
- menus;
- context menu;
- title bar;
- bordas;
- resize;
- window dragging;
- overlays/scripts mpv que capturam mouse.

Se o OSC ou script já consumir o clique, não duplicar o evento no host.

Reutilize `IsMouseInOsc()` ou mecanismo equivalente existente quando aplicável.

---

## Conflito da tecla P

Hoje existe indicação de que `p` está sendo usado para `show-progress`.

A nova regra de produto é:

- `P/p` deve ser Play/Pause.

Antes da alteração:

1. confirme os bindings ativos;
2. identifique se há distinção intencional entre `p` e `P`;
3. confirme como o parser trata maiúscula/minúscula;
4. remova ou realoque `show-progress` apenas se necessário;
5. não invente um novo atalho para `show-progress` sem avaliar conflitos;
6. documente no relatório final qual binding ficou responsável por mostrar progresso.

O mais importante é não deixar dois comandos diferentes competindo pela mesma tecla.

---

## Compatibilidade com input.conf do usuário

Este ponto é obrigatório.

O MPV.NET permite customização via `input.conf`.

A implementação não deve sobrescrever silenciosamente a escolha do usuário.

Analise:

- como defaults são mesclados em `InputConf.GetBindings()`;
- como overrides do usuário substituem defaults;
- como o editor de input mostra bindings;
- como `GetContent()` serializa defaults;
- se usuários que já personalizaram `MBTN_Left`, `MBTN_Left_DBL`, `p`, `P` ou `Space` continuarão tendo prioridade.

Regra:

**binding explicitamente definido pelo usuário deve prevalecer sobre o novo default.**

Não migre ou reescreva `input.conf` do usuário sem necessidade técnica comprovada.

Se alguma migração for inevitável:

- criar backup;
- torná-la idempotente;
- proteger customizações;
- cobrir com testes.

---

## Fullscreen: área protegida contra regressão

Evite modificar:

- `MainForm.Fullscreen.cs`
- `CycleFullscreen`
- restauração de janela;
- `_wasMaximized`;
- bordas;
- Media Transport suspend/resume;
- lógica atual de tamanho/posição.

Apenas modificar essa área se existir prova objetiva de que o fluxo atual impede o novo comportamento.

Se precisar alterar, justificar no relatório e criar testes/regressão específicos.

---

## Play/Pause: fonte única de verdade

Localize e valide a implementação de:

`script-message-to mpvnet play-pause`

Confirme como ela manipula:

- propriedade `pause`;
- estado sem mídia;
- end-of-file;
- loading/buffering;
- áudio;
- vídeo;
- stream;
- playlist;
- integração SMTC.

Mouse, `P`, `Space`, botão multimídia e SMTC devem convergir para o mesmo comportamento sempre que possível.

Não duplicar lógica.

---

## Escopo de baixo risco

Além dos comportamentos solicitados, faça apenas um levantamento dos padrões comuns de players modernos.

Você pode analisar, mas **não implementar automaticamente** recursos adicionais como:

- J/K/L;
- duplo clique em lados da tela para seek;
- clique e arraste para seek;
- gestos;
- overlays novos;
- animações;
- atalhos extras;
- mudança de volume por teclas diferentes;
- mini player;
- PiP;
- mudanças no OSC.

No relatório final, liste sugestões futuras em:

- baixo risco;
- médio risco;
- alto risco.

Nenhuma dessas sugestões adicionais deve entrar neste commit sem necessidade direta.

---

## Estratégia de implementação

### Fase 1 — baseline

Antes de mudar código:

1. criar branch dedicada, por exemplo:
   `feature/player-modern-input-controls`

2. executar:
   - restore;
   - build;
   - testes existentes;
   - smoke test disponível.

3. registrar qualquer falha que já existia antes.

### Fase 2 — análise

Produzir antes da alteração:

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Bindings atuais:

Fluxo atual de Play/Pause:

Fluxo atual de fullscreen:

Como o mpv diferencia clique simples e duplo clique:

Conflitos identificados:

Mudança mínima proposta:

Riscos:

Plano de teste:
```

### Fase 3 — implementação mínima

Prioridade:

1. bindings;
2. testes;
3. somente depois, se comprovadamente necessário, código de arbitragem single/double click.

Não refatorar código adjacente.

### Fase 4 — testes

Adicionar cobertura automatizada quando tecnicamente possível para:

- default `MBTN_Left`;
- default `MBTN_Left_DBL`;
- `Space`;
- `p/P`;
- overrides por `input.conf`;
- ausência de duplicidade de bindings;
- serialização/parsing;
- preservação dos demais bindings.

Se a distinção single/double click for implementada em classe própria, tornar a lógica testável independentemente da UI sempre que possível.

---

## Matriz obrigatória de testes manuais

### Reprodução

Testar com:

- vídeo local;
- áudio local;
- URL/stream HTTP;
- mídia online via yt-dlp/YouTube quando disponível;
- playlist;
- mídia pausada;
- buffering;
- troca de arquivo.

### Mouse

Verificar:

- clique simples reproduzindo → pausa;
- clique simples pausado → play;
- duplo clique em janela → fullscreen;
- duplo clique fullscreen → janela;
- duplo clique não altera pause;
- botão direito continua abrindo menu;
- roda continua controlando volume;
- botão do meio continua com comportamento atual;
- botões laterais continuam funcionando;
- clique/drag continua movendo janela quando permitido;
- resize permanece funcionando;
- OSC continua clicável;
- timeline continua funcionando.

### Teclado

Verificar:

- p;
- P;
- Space;
- Enter;
- f;
- Esc;
- setas;
- atalhos de volume;
- atalhos de legendas;
- atalhos de tracks;
- menu;
- atalhos personalizados do `input.conf`.

### Janela

Testar:

- normal;
- maximizada;
- fullscreen;
- sair de fullscreen;
- múltiplos monitores se disponível;
- DPI 100%, 125%, 150% quando possível;
- borda habilitada/desabilitada.

### Integrações

Testar:

- SMTC Play/Pause;
- taskbar;
- Media Transport;
- OSC;
- cursor auto-hide;
- menu de contexto;
- shutdown durante estado pausado;
- shutdown durante possível timer de single-click;
- abrir nova mídia após pausa.

---

## Critérios de aceite

A tarefa somente está concluída quando:

1. clique simples alterna Play/Pause corretamente;
2. duplo clique alterna fullscreen corretamente;
3. duplo clique não altera o estado Play/Pause;
4. P/p alterna Play/Pause;
5. Space continua Play/Pause;
6. customizações existentes de `input.conf` continuam tendo prioridade;
7. menu, OSC, mouse wheel, botões laterais e drag continuam funcionando;
8. SMTC continua funcionando;
9. fullscreen mantém o comportamento anterior;
10. nenhum warning/error novo relevante aparece;
11. build e testes passam;
12. o diff permanece pequeno e diretamente relacionado à tarefa.

---

## Proteções obrigatórias contra regressão

Não:

- atualizar mpv/libmpv nesta tarefa;
- atualizar yt-dlp nesta tarefa;
- trocar framework;
- alterar arquitetura;
- refatorar `MainForm` amplamente;
- reescrever fullscreen;
- mudar SMTC sem necessidade;
- adicionar dependência externa;
- criar thread dedicada;
- bloquear UI;
- usar delays arbitrários;
- capturar mouse globalmente;
- quebrar `input.conf`;
- mudar bindings não relacionados;
- alterar comportamento de seek/volume/playlist sem solicitação.

Se alguma dessas ações parecer necessária, parar a implementação desse ponto e registrar a justificativa no relatório antes de prosseguir.

---

## Documentação

Atualizar somente documentos existentes pertinentes, por exemplo:

- `docs/ATALHOS.md`
- `docs/manual.md`

Documentar de forma simples:

- clique esquerdo: Play/Pause;
- duplo clique esquerdo: Fullscreen;
- P: Play/Pause;
- Space: Play/Pause.

Não criar documentação redundante.

---

## Commit

Depois de validar tudo:

1. revisar `git diff`;
2. confirmar que só existem alterações relacionadas;
3. executar testes finais;
4. criar commit claro, por exemplo:

`feat(player): add safe click and keyboard playback controls`

5. fazer push da branch;
6. não fazer merge automático em `main` se houver qualquer dúvida de regressão.

---

## Relatório final obrigatório

Entregar:

```text
Resumo da implementação:

Arquivos alterados:

Bindings anteriores:

Bindings finais:

Como o clique simples foi implementado:

Como o duplo clique foi preservado:

Foi necessário arbitrador single/double click? Por quê?

Como P/p foi tratado:

O que aconteceu com show-progress:

Compatibilidade com input.conf:

Testes automatizados executados:

Testes manuais executados:

Resultados:

Riscos residuais:

Sugestões futuras não implementadas:

Commit:

Branch:
```

A prioridade desta tarefa é UX melhor **sem sacrificar a estabilidade atual do player**.
