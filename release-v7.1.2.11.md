# Fork WandersondeSouza - v7.1.2.11 (2026-06-08)

## Reproducao e inicializacao

- Ajustado o fluxo de abertura e reproducao para lidar melhor com arquivos, playlists vazias e caminhos normalizados antes de chegar ao mpv/libmpv.
- Refinado o tratamento de argumentos de linha de comando e do arranque da aplicacao para reduzir falhas de timeout e manter o comportamento previsivel.
- Corrigida a abertura do menu de contexto por clique direito ao forcar a posicao do popup no ponto do mouse.
- Tornada tolerante a montagem dos rotulos padrao do menu para ignorar comandos repetidos no mapeamento de atalho, evitando falha de inicializacao.
- Mantida a separacao interna de responsabilidades em `Player.cs` e `MainForm.cs` para preservar o contrato existente sem refatoracao ampla.

## Interface e suporte

- O item `Support` do menu agora abre o cliente de e-mail padrao com `wanderson_souza@hotmail.com` e o menu gerado foi atualizado para regenerar essa entrada nas configuracoes existentes quando a versao do gerador muda.
- Elevada a versao do gerador do `menu.conf` para substituir instalacoes antigas que ainda tinham a URL `web.libera.chat/#mpv` no item `Support`.
- Ajustada a inicializacao do idioma da interface para usar o idioma do Windows quando houver mapeamento suportado, cair para ingles quando nao houver e respeitar parametros ou configuracoes explicitas.

## Idiomas e midia

- Melhorado o registro de informacoes de inicializacao e reproducao para facilitar diagnostico sem alterar o caminho principal de execucao.
- Ajustada a leitura das dependencias nativas para buscar o pacote correto de DLLs e manter a compatibilidade com o binario esperado.
- Ampliada a cobertura automatizada para parser de argumentos, auto-load de pasta, associacoes de imagens, normalizacao de caminhos e selecao de idioma.

## Empacotamento e instalador

- Reforcada a preparacao de build e release para validar melhor o runtime x64 e o pacote correto de dependencias nativas antes de gerar os artefatos.
- Atualizados os scripts de ZIP, instalador e release para seguir o mesmo fluxo de preparacao de dependencias e manter a publicacao consistente.
- Mantida a validacao dos artefatos gerados com `libmpv-2.dll`, FFmpeg, `ffprobe.exe`, `ffplay.exe` e `yt-dlp.exe`.
