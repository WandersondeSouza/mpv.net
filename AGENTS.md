# AGENTS.md

## Identidade do projeto

O **mpv.net** é um player de mídia para Windows com interface gráfica moderna, baseado no **mpv/libmpv**.

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

## Arquivos que devem ser analisados primeiro

- README.md
- docs/manual.md
- docs/changelog.md
- arquivos relacionados à funcionalidade alterada

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

## Formato recomendado antes de alterar código

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Problema encontrado:

Mudança proposta:

Riscos:

Plano de teste:
```

## Prioridade atual do fork

1. documentação técnica;
2. tradução parcial para português brasileiro;
3. entendimento da arquitetura;
4. preparação para uso com agentes de IA;
5. melhorias futuras.
