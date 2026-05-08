# Fluxo de Inicialização do mpv.net

## Objetivo

Documentar o fluxo de inicialização da aplicação para facilitar manutenção, debug e futuras refatorações.

> Versão inicial. Deve ser validada após inspeção completa da árvore de código.

---

# Fluxo esperado de inicialização

1. Processo do executável é iniciado.
2. Argumentos de linha de comando são lidos.
3. Configurações iniciais são resolvidas.
4. Pasta de configuração é localizada.
5. Integração com mpv/libmpv é preparada.
6. Janela principal é criada.
7. Eventos do mpv/libmpv são registrados.
8. UI passa a refletir estado do player.
9. Arquivos/URLs informados por linha de comando são carregados.

---

# Pontos que precisam ser confirmados no código

- arquivo de entry point;
- classe principal de aplicação;
- criação da janela principal;
- inicialização de libmpv;
- carregamento de argumentos;
- carregamento de configuração;
- tratamento de instância única;
- comportamento ao abrir arquivos via Explorer.

---

# Riscos durante alterações

## Instância única

Mudanças podem quebrar abertura de arquivos por associação do Windows.

## Configuração inicial

Mudanças podem quebrar modo portátil e `MPVNET_HOME`.

## libmpv

Mudanças podem impedir inicialização do player.

## UI

Mudanças podem causar janela sem renderização, fullscreen incorreto ou menu inativo.

---

# Checklist de teste

- [ ] abrir sem argumentos;
- [ ] abrir com arquivo local;
- [ ] abrir com URL;
- [ ] abrir via associação do Windows;
- [ ] abrir múltiplos arquivos;
- [ ] validar modo single instance;
- [ ] validar pasta de configuração;
- [ ] validar saída no terminal.
