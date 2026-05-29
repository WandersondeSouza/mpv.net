# Próximos Trabalhos

Documento curto para orientar a manutenção do fork `mpv.net`.

Os itens aqui são pendências ou verificações em aberto. Quando algo for consolidado e validado, mova o histórico para `docs/changelog.md`.

## Desenvolvimento prioritario

- validar o início maximizado sem entrar em fullscreen, para separar estado de janela de modo de exibição;
- confirmar se o carregamento de atalhos continua estável quando o perfil usa condições dinâmicas;
- verificar o fluxo de áudio baixo em cenários reais e comparar com a execução padrão do `mpv`;
- revisar suporte a LUT de ponta a ponta, não apenas a exposição de opções no editor de configuração;
- fechar os pontos pendentes de abertura da janela e do estado visual no arranque;
- reforçar a estabilidade do carregamento de configuração e de atalhos durante a inicialização;
- validar entrada de streaming e URLs em protocolos diferentes, incluindo os casos ainda tratados como suporte parcial;
- preservar a persistência de volume, janela e preferências do usuário entre sessões;
- atualizar a documentação sempre que o comportamento observado mudar.

## Navegação técnica

Para cada item acima, a primeira leitura recomendada é:

- `docs/developer/architecture.md` quando o impacto for amplo;
- `docs/developer/source-audit.md` quando houver dúvida sobre a estrutura real;
- `docs/developer/configuration-flow.md` e `docs/developer/startup-flow.md` quando a dúvida envolver inicialização ou configuração;
- `docs/developer/project-map.md` quando o problema for de módulos e arquivos.

