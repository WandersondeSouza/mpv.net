# Plano de Trabalho para o Codex - Fork mpv.net

Este documento registra apenas o trabalho ainda em aberto.
O que já foi corrigido fica no `docs/changelog.md` e, quando fizer sentido, em documentos técnicos específicos.

## Regra geral

- Não repetir no plano o que já foi concluído.
- Manter aqui só pendências reais, tarefas ativas e validações pendentes.
- Quando um item for fechado, mover o registro final para o changelog e remover o item do plano.

## Foco atual

- validar pendências manuais;
- corrigir bugs pequenos e comprovados;
- manter a documentação objetiva e sem duplicação;
- evitar grandes refatorações;
- preservar compatibilidade com mpv/libmpv.

## Etapas ativas

### Etapa 1 - Pacote portátil

Status: pendente validar instalador, publicação GitHub e revisão manual completa da UI.

Tarefas:

- validar instalador;
- validar publicação GitHub;
- validar fullscreen, menu, atalhos, persistência de configuração e temas;
- revisar a documentação do modo portátil se algo ainda estiver divergindo do comportamento real.

### Etapa 2 - Atalhos e `input.conf`

Status: pendente validação visual interativa da janela.

Tarefas:

- validar visualmente o aviso do editor ao tentar salvar duplicidade;
- confirmar a experiência final do usuário ao editar atalhos.

### Etapa 3 - `thumbfast` no portátil

Status: pendente validação visual de thumbnails em uso real.

Tarefas:

- validar thumbnails do `thumbfast` em uma UI de usuário final;
- atualizar a documentação apenas se surgir um caso novo de uso.

### Etapa 4 - Caminhos longos no Windows

Status: pendente validar `Explorer`, associação de arquivo e menu `Open Files...`.

Tarefas:

- validar abertura via `Explorer`;
- validar abertura por associação de arquivo;
- validar menu `Open Files...`;
- registrar apenas o que ainda falhar em um documento técnico separado, se necessário.

### Etapa 5 - `pause=yes` e autoplay

Status: pendente confirmação nos fluxos de abertura do app.

Tarefas:

- testar abertura pelo menu do app;
- testar arrastar e soltar;
- testar associação de arquivo;
- testar linha de comando;
- verificar se `pause=yes` permanece após carregar o próximo item;
- aplicar correção pequena somente se o problema reaparecer.

### Etapa 6 - Bugs visuais simples

Status: pendente reprodução visual confiável.

Tarefas:

- verificar renderização ao fechar;
- testar modo escuro;
- comparar com `mpv.exe` quando fizer sentido;
- evitar mexer no ciclo de vida completo da janela sem necessidade.

### Etapa 7 - Higiene de manutenção

Status: pendente para próximas revisões.

Tarefas:

- revisar o checklist de release após novas validações reais;
- manter o roadmap separado entre concluído, pendente e futuro;
- manter fora desta fase itens grandes como `uosc`, `LUT`, `AI upscaling`, IPTV/media center e mudanças profundas em `yt-dlp`.

## Documentos relacionados

- [README do fork](../README.md)
- [Modo portátil](PORTATIL.md)
- [Configuração](CONFIGURACAO.md)
- [Atalhos](ATALHOS.md)
- [Build e release](BUILD.md)
- [Roadmap](ROADMAP.md)
- [Prompt de proximas melhorias](../.ai/prompts/next-improvements.md)
- [Template de bug](../.github/ISSUE_TEMPLATE/bug_report.md)
- [Template de melhoria](../.github/ISSUE_TEMPLATE/feature_request.md)

