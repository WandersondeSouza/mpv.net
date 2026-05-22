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

### Etapa 1 - Higiene de documentação e status

Status: em andamento.

Tarefas:

- manter `docs/PLANO-CODEX.md`, `docs/ROADMAP.md` e `docs/release-checklist-ptbr.md` alinhados ao estado real do fork;
- separar com clareza o que já foi validado do que ainda depende de teste manual;
- remover duplicações entre plano, roadmap e changelog.

### Etapa 2 - Validações da release e do pacote portátil

Status: pendente confirmar publicacao GitHub e validar novamente a instalacao em ambiente isolado.

Tarefas:

- validar instalador;
- validar publicação GitHub;
- validar o ZIP portátil e o smoke test do executável extraído;
- manter a documentação de build, modo portátil e checklist coerente com o layout real do pacote.

### Etapa 3 - Bugs pequenos e confirmados

Status: pendente reprodução visual confiável.

Tarefas:

- verificar renderização ao fechar;
- testar modo escuro e diferenças visuais básicas;
- comparar com `mpv.exe` apenas quando isso ajudar a isolar regressão;
- evitar mexer no ciclo de vida completo da janela sem necessidade.

### Etapa 4 - Input, thumbfast e caminhos longos

Status: pendente validação manual direcionada.

Tarefas:

- validar o editor de atalhos e conflitos de `input.conf`;
- confirmar a documentação do `thumbfast` no layout real do pacote portátil;
- fechar a validação de long paths em comandos, segunda instância e abertura via Explorer.

### Etapa 5 - Autoplay e higiene final

Status: pendente para próximas revisões.

Tarefas:

- revisar o comportamento de `reset-on-next-file=pause` quando `pause=yes`;
- atualizar o changelog apenas com o que estiver realmente fechado;
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
