# Scripts de manutenção

Resumo dos scripts usados no fluxo do fork.

## Principais scripts

- `src/Tools/build-release-package.ps1`: gera ZIP portátil, instalador e release.
- `src/Tools/prepare-native-dependencies.ps1`: prepara as dependências nativas ao lado do executável.
- `src/Tools/validate-native-dependencies.ps1`: valida os binários obrigatórios antes da publicação.
- `src/Tools/update-mpv-runtime.ps1`: auxilia na atualização do runtime do mpv.

## Regra

Se o script muda o pacote final, a documentação de build, portable e release também deve ser atualizada.
