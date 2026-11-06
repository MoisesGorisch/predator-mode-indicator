![Visão geral: estados Eco ativado e desativado na área de notificação](docs/overview.svg)

# Predator Mode Indicator

Indicador na área de notificação do Windows que acompanha o **perfil salvo pelo PredatorSense**, com atualização a cada cinco segundos.

![Print real do velocímetro verde na área de notificação](docs/tray-balanced.png)

| Ícone | Estado |
| --- | --- |
| Lua branca | Silencioso |
| Velocímetro verde | Equilibrado |
| Raio roxo | Desempenho |
| Foguete magenta | Turbo |
| Folha laranja | Eco |
| Bolinha cinza | Estado indisponível |

Com o carregador original reconhecido pelo PredatorSense, o indicador lê o perfil salvo no cenário ativo. Em USB-C ou bateria, lê o sinal de Eco. Quando o registro do adaptador está indisponível, o texto do ícone informa o estado de Eco com perfil desconhecido.

![Artes dos ícones e dimensões](docs/icons-preview.png)

## Instalação e uso

Baixe e extraia o ZIP em [Releases](https://github.com/MoisesGorisch/predator-mode-indicator/releases) e execute `IndicadorPredator.exe`. Não precisa de AutoHotkey, instalação ou administrador. Se o ícone estiver na setinha de ícones ocultos, arraste-o para a área visível. O Windows controla sua posição.

Passe o mouse para ver o estado. Clique com o botão direito para atualizar manualmente, ver detalhes ou sair. O programa não cria uma janela sobre jogos ou vídeos.

Para iniciar com o Windows, pressione `Win+R`, digite `shell:startup` e coloque ali um **atalho** para o executável. Não mova o executável depois de criar o atalho. Para desativar, remova apenas o atalho. A inicialização automática não é ativada ao simplesmente abrir o programa.

## Origem e limites

O programa lê o `%ProgramData%\OEM\PredatorSense\ProfilePool\config.json` e o estado do adaptador nos registros locais do PredatorSense. O ícone acompanha o estado salvo pelo aplicativo e depende de ele registrar as mudanças.

Testado no **PHN16-71** com **PredatorSense 5.0.1463**. Os quatro perfis do carregador original passaram nos testes de leitura. Desempenho foi confirmado no indicador em execução. A compatibilidade com outros modelos e versões segue em aberto. Requer Windows com .NET Framework 4.x.

## Compilar

No Windows PowerShell:

```powershell
.\build.ps1
.\test.ps1
```

O resultado fica em `dist`. Os ícones estão incorporados ao executável; os SVGs e PNGs ficam em `assets/icons` para edição. Mais detalhes no [README em inglês](README.md).

Projeto independente, sem vínculo com a Acer. Código e artes próprias sob [licença MIT](LICENSE).
