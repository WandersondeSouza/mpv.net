# AGENTS.md

## Identidade do projeto

O **MPV.NET Media Player** é um player de mídia para Windows com interface gráfica moderna, baseado no **mpv.net** e no **mpv/libmpv**.

A principal regra de manutenção é preservar a compatibilidade com o mpv sempre que possível.

## Objetivo deste arquivo

Este arquivo orienta agentes de IA, Codex, GitHub Copilot e ferramentas automatizadas que venham a analisar ou modificar este repositório.

## Regras principais

1. Não quebrar compatibilidade com o mpv.
2. Evitar refatorações amplas sem solicitação explícita.
3. Preferir mudanças pequenas e documentadas.
4. Atualizar documentação sempre que alterar comportamento.
5. Preservar compatibilidade com arquivos de configuração existentes.
6. Antes de alterar código, entender o comportamento atual.
7. Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes e evitando criar arquivos redundantes.

## Arquivos que devem ser analisados primeiro

- README.md
- docs/manual.md
- docs/guia-operacional.md
- documentação técnica em docs/developer/
- arquivos relacionados à funcionalidade alterada

## Artefatos de IA do fork

Além deste arquivo, o repositório possui materiais auxiliares em `.ai/`:

- `.ai/skills/mpvnet-maintainer.md`: skill base para manutenção conservadora do fork;
- `.ai/agents/`: perfis de agentes por área crítica;
- `.ai/prompts/`: prompts reutilizáveis para auditorias, bugs, configuração, libmpv, release e documentação;
- `.ai/mcp/README.md`: recomendações de MCPs úteis para trabalhar no projeto.

Esses arquivos não substituem a análise do código atual. Eles servem como ponto de partida para agentes trabalharem com menos risco.

## Áreas críticas

### Integração com mpv/libmpv

Mudanças nessa área são consideradas de alto risco.

### Configuração

A lógica de carregamento da pasta de configuração deve permanecer compatível.

### Interface gráfica

Mudanças de UI devem validar:

- tema claro/escuro;
- DPI;
- tela cheia;
- atalhos;
- menu de contexto.

### Testes e validação

Antes de alterar comportamento em parser, caminhos, playlists, títulos, logs, configuração, seleção de idioma ou MediaInfo, verificar se existe cobertura em `src/MpvNet.Tests/Program.cs` e ampliá-la quando a mudança criar um caso novo.

Para mudanças em UI ou integração com libmpv, combinar build/testes automatizados com checklist manual de execução: arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento.

## Formato recomendado antes de alterar código

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Problema encontrado:

Mudança proposta:

Riscos:

Plano de teste:
```

## Fonte de verdade e estado do projeto

- Confirme o comportamento no código e nos scripts da branch atual; este arquivo
  define princípios de manutenção, não um retrato de funcionalidades ou
  prioridades temporárias.
- Use `README.md` como índice, `docs/manual.md` para uso, `docs/guia-operacional.md`
  para operações e `docs/developer/` para detalhes técnicos.
- Trate relatórios datados e prompts em `.ai/prompts/` como registros de tarefas
  anteriores. Eles não comprovam o estado atual nem substituem esta leitura.
- Registre como pendente apenas uma validação ou atividade que ainda não tenha
  evidência concluída; indique ambiente e escopo quando aplicável.
