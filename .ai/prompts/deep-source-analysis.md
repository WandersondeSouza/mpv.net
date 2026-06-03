# Prompt — Auditoria profunda de arquitetura e código-fonte

Analise profundamente este repositório.

Objetivo:

Entender a arquitetura técnica do projeto MPV.NET Media Player para manutenção de longo prazo, identificar acoplamentos e preparar mudanças grandes com risco controlado.

---

## Etapa 1 — Estrutura

Identifique:

- solução `.sln`;
- projetos `.csproj`;
- estrutura de pastas;
- dependências;
- assets;
- arquivos de build;
- arquivos de release.

Confirme também:

- `docs/developer/architecture.md`;
- documentação técnica relacionada aos módulos analisados.

---

## Etapa 2 — Inicialização

Explique:

- entry point;
- inicialização da aplicação;
- carregamento de configuração;
- criação da janela principal;
- inicialização da integração com mpv/libmpv.

---

## Etapa 3 — Integração com mpv/libmpv

Mapeie:

- wrappers;
- chamadas nativas;
- propriedades;
- eventos;
- comandos;
- sincronização.

Explique o fluxo de comunicação.

---

## Etapa 4 — Interface gráfica

Explique:

- janela principal;
- fullscreen;
- menu de contexto;
- overlays;
- temas;
- controles;
- mouse;
- teclado.

---

## Etapa 5 — Sistema de comandos

Explique:

- parser;
- `input.conf`;
- atalhos;
- menu;
- comandos específicos do mpv.net.

---

## Etapa 6 — Configuração

Explique:

- resolução da pasta de configuração;
- carregamento de arquivos;
- persistência;
- compatibilidade.

---

## Etapa 7 — Extensões e scripts

Explique:

- Lua;
- JavaScript;
- extensões .NET;
- pontos de extensão.

---

## Etapa 8 — Riscos técnicos

Identifique:

- áreas frágeis;
- acoplamentos;
- dependências críticas;
- riscos de regressão;
- gargalos de manutenção.

## Etapa 9 — Limites da análise

Se a descoberta mostrar que o trabalho é mais bem dividido por área, pare e proponha um recorte menor antes de editar.

---

## Entrega esperada

Entregue:

1. mapa técnico completo;
2. fluxo arquitetural;
3. módulos principais;
4. classes críticas;
5. riscos;
6. sugestões de documentação;
7. sugestões de refatoração segura.

Antes de alterar qualquer código:

- resumir entendimento atual;
- listar arquivos analisados;
- listar riscos;
- propor plano incremental.
- atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes.


