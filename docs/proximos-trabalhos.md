# Próximos Trabalhos

Documento curto para orientar a manutenção do fork `mpv.net`.

Os itens aqui são pendências ou verificações em aberto. Quando algo for consolidado e validado, mova o histórico para `docs/changelog.md`.

## Foco imediato

- fechar a cadeia de distribuição e atualização dos binários de terceiros;
- travar versões e validar integridade com hash e/ou assinatura antes de copiar para o pacote final;
- revisar `src/Tools/prepare-native-dependencies.ps1`, `src/Tools/build-release-package.ps1` e `src/Tools/update-mpv-runtime.ps1` como a mesma cadeia de confiança;
- documentar no fluxo de release de onde cada dependência vem, como é validada e o que deve falhar quando a origem mudar.

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

## Itens já entendidos e não prioritários

- carregamento de extensões locais em `portable_config/extensions`;
- IPC local por `WM_COPYDATA`;
- abertura explícita de arquivos, URLs e associações pelo usuário;
- carregamento legado de `AviSynth.dll` quando o arquivo existe no ambiente local.

## Navegação técnica

Para cada item acima, a primeira leitura recomendada é:

- `docs/developer/architecture.md` quando o impacto for amplo;
- `docs/developer/configuration.md` quando a dúvida envolver inicialização ou configuração;
- `docs/developer/build-release.md` quando a dúvida envolver empacotamento, download ou release;
- `docs/developer/mpv-integration.md` quando a dúvida envolver `libmpv`, IPC com o player ou carregamento de mídia.
