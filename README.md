# Sangria Companion 2.0.1 — estabilização modular

Esta é a primeira etapa da reconstrução modular.

## Correção crítica

O BepInEx rejeitava a versão anterior porque o atributo do plugin usava uma versão inválida (`1.2.0.3-emergency-launcher`). Agora o atributo usa SemVer válido:

`2.0.1`

## Preservado

- Dashboard personalizável
- Catálogo de bosses por atos, pesquisa, favoritos e alertas
- Eventos e horário do servidor
- Rastreador de estado/respawn e HUD compacta
- Rastreador experimental de bosses móveis
- Coleta, receitas, árvore de fabricação e HUD extra
- Leitura experimental do inventário
- Descoberta de receitas reais
- Tema Sangria Falls
- Bloqueio de entrada durante interação
- Notificações mesmo com painel fechado

## Nova proteção modular

Cada serviço roda isoladamente. Se inventário ou receitas falharem, o botão SC, bosses, eventos e notificações continuam funcionando.

## Compilar

Feche o V Rising e execute `COMPILAR_E_INSTALAR.bat`.

Depois confira `BepInEx/LogOutput.log`. Deve existir:

`Loading [Sangria Companion 2.0.1]`
