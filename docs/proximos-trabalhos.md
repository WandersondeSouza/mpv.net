# Proximos Trabalhos

Documento curto para orientar a manutencao do fork `mpv.net`.

## Em investigacao

- validar o comportamento de inicio maximizado sem entrar em fullscreen;
- confirmar se o carregamento de atalhos continua estavel quando o arquivo de perfil usa condicoes dinamicas;
- verificar o fluxo de audio baixo em cenarios reais e comparar com a execucao padrao do `mpv`;
- revisar suporte a LUT de forma completa, nao apenas a exposicao de opcoes no editor de configuracao.

## Desenvolvimento prioritario

- fechar os pontos pendentes de inicializacao da janela e do estado visual;
- reforcar a estabilidade do carregamento de configuracao e de atalhos;
- validar o comportamento de streaming e entrada de URLs em diferentes protocolos;
- manter compatibilidade com caminhos longos no Windows;
- preservar a persistencia de volume, janela e preferencias do usuario;
- atualizar a documentacao sempre que o comportamento observado mudar.

## Trabalho ja consolidado no fork

- suporte a caminhos longos ja existe;
- protocolos de streaming comuns ja sao reconhecidos;
- o estado de janela maximizada ja e tratado;
- o fluxo de traducoes e configuracao local ja esta documentado.

## Proximo passo pratico

- reproduzir os cenarios pendentes com base real de uso;
- confirmar o que e bug, o que e suporte parcial e o que e apenas diferenca de comportamento;
- implementar primeiro o que afeta abertura da janela, atalhos e reproducao;
- validar as mudancas antes de mover o texto para a documentacao final do projeto.

