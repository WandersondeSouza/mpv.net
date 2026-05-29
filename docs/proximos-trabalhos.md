# Proximos Trabalhos

Documento curto para orientar a manutencao do fork `mpv.net`.

## Desenvolvimento prioritario

- validar o inicio maximizado sem entrar em fullscreen, para separar estado de janela de modo de exibicao;
- confirmar se o carregamento de atalhos continua estavel quando o perfil usa condicoes dinamicas;
- verificar o fluxo de audio baixo em cenarios reais e comparar com a execucao padrao do `mpv`;
- revisar suporte a LUT de ponta a ponta, nao apenas a exposicao de opcoes no editor de configuracao;
- fechar os pontos pendentes de abertura da janela e do estado visual no arranque;
- reforcar a estabilidade do carregamento de configuracao e de atalhos durante a inicializacao;
- validar entrada de streaming e URLs em protocolos diferentes, incluindo os casos ainda tratados como suporte parcial;
- preservar a persistencia de volume, janela e preferencias do usuario entre sessoes;
- atualizar a documentacao sempre que o comportamento observado mudar.
