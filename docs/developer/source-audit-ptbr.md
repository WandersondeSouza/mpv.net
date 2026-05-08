# Auditoria Inicial do Código-Fonte

## Objetivo

Este documento registra a etapa inicial de auditoria do código-fonte do fork `WandersondeSouza/mpv.net`.

A finalidade é identificar estrutura real, projetos, pontos de entrada e áreas de manutenção.

---

# Status da auditoria

A documentação pública e os arquivos principais de documentação foram encontrados, porém a busca indexada do GitHub não retornou imediatamente arquivos como:

- `.sln`;
- `.csproj`;
- `Program.cs`;
- arquivos principais de inicialização.

Isso significa que a auditoria técnica profunda ainda precisa de validação adicional por clonagem local ou inspeção completa da árvore de arquivos.

---

# Documentos já confirmados

Foram confirmados:

- `README.md`;
- `docs/manual.md`;
- documentação de usuário;
- informações sobre instalação;
- informações sobre configuração;
- informações sobre comandos;
- informações sobre extensões.

---

# Entendimento técnico atual

Com base na documentação existente, o projeto é composto por estas responsabilidades principais:

1. aplicação desktop Windows;
2. frontend gráfico moderno;
3. integração com mpv/libmpv;
4. sistema de configuração compatível com mpv;
5. comandos específicos do mpv.net;
6. atalhos e menu de contexto;
7. suporte a scripts Lua/JavaScript;
8. suporte a extensões .NET;
9. temas;
10. empacotamento para distribuição.

---

# Arquivos que precisam ser localizados

Durante a próxima etapa, localizar:

## Solução e projetos

- arquivo `.sln`;
- arquivos `.csproj`;
- arquivos de build;
- arquivos de empacotamento.

## Inicialização

- ponto de entrada;
- inicialização da aplicação;
- criação da janela principal;
- carregamento inicial de configuração.

## Integração mpv/libmpv

- wrapper ou camada de chamada nativa;
- gerenciamento de propriedades;
- envio de comandos;
- recebimento de eventos.

## Interface gráfica

- janela principal;
- menus;
- controles;
- fullscreen;
- temas.

## Comandos e input

- parser de comandos;
- input.conf;
- atalhos;
- menu de contexto.

---

# Riscos já identificados

## Risco 1 — Alterar compatibilidade com mpv

Qualquer alteração que modifique comandos, propriedades ou opções pode quebrar comportamento esperado.

## Risco 2 — Alterar configuração

Mudanças no carregamento de `mpv.conf`, `mpvnet.conf` ou `input.conf` podem quebrar instalações existentes.

## Risco 3 — Alterar fullscreen/UI

Mudanças de UI podem quebrar fullscreen, menu de contexto e comportamento do mouse.

## Risco 4 — Dependências nativas

Dependências do mpv/libmpv podem dificultar build e debug.

---

# Próximos passos técnicos

1. Clonar o repositório localmente.
2. Rodar busca por arquivos:

```bash
find . -name "*.sln" -o -name "*.csproj" -o -name "Program.cs" -o -name "*.props" -o -name "*.targets"
```

No PowerShell:

```powershell
Get-ChildItem -Recurse -Include *.sln,*.csproj,Program.cs,*.props,*.targets
```

3. Identificar entry point.
4. Identificar projeto principal.
5. Identificar dependências nativas.
6. Atualizar este documento com dados reais.

---

# Resultado esperado da próxima auditoria

Após inspeção completa, este documento deve conter:

- mapa real de pastas;
- lista real de projetos;
- dependências;
- fluxo de inicialização;
- classes principais;
- fluxo mpv/libmpv;
- pontos de extensão;
- riscos técnicos por módulo.
