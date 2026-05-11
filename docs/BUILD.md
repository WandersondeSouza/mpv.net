# Build e release

Este documento e uma orientacao inicial para estudar o build do fork do mpv.net.

> Status: pendente de validacao em um build real.

## Plataforma

O mpv.net e um projeto para Windows.

O build deve ser feito com Visual Studio/.NET conforme a estrutura atual do projeto. A versao exata recomendada do Visual Studio, do SDK .NET e das dependencias nativas ainda precisa ser validada neste fork.

## Como abrir o projeto

Orientacao inicial:

1. Usar Windows.
2. Abrir a solucao/projeto no Visual Studio.
3. Restaurar pacotes NuGet.
4. Compilar em Debug antes de tentar empacotamento ou release.

Comandos exatos de terminal ainda estao pendentes de validacao.

## Scripts relacionados

Existem scripts em:

```text
src/Tools
```

Scripts relevantes para analise futura:

- `src/Tools/release-mpv.net.ps1`: relacionado ao fluxo de build/release.
- `src/Tools/update-mpv.ps1`: relacionado a atualizacao de mpv/libmpv.

Estes scripts nao foram alterados nesta etapa.

## Pendencias de validacao

- Versao recomendada do Visual Studio.
- Versao recomendada do SDK .NET.
- Comando exato de build.
- Fluxo de release.
- Fluxo de pacote portatil.
- Dependencias nativas necessarias para executar o player compilado.
- Relacao exata entre build do mpv.net e atualizacao de mpv/libmpv.

Este documento deve ser refinado depois de um teste real de build.
