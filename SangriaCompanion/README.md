# Sangria Companion v1.2.0.2 — HUD Startup Fix

Correções desta versão:

- remove a dependência do patch Harmony em `ClientChatSystem.OnUpdate`;
- localiza o World do cliente de forma segura durante o `Update`;
- garante que uma falha em patch opcional não impeça a HUD de ser criada;
- mantém o botão `SC` visível mesmo se a configuração `Enabled` estiver inconsistente;
- preserva tema Sangria, notificações, coleta, receitas e rastreamento móvel experimental.

Feche o V Rising antes de executar `COMPILAR_E_INSTALAR.bat`.


## Versão 2.3.0

- Corrigido bloqueio de movimentação com HUDs abertas.
- Removido reset global dos eixos WASD.
- Adicionados controles globais e por categoria para avisos.
- Adicionado silêncio temporário por 1 hora ou até reiniciar.
- Adicionados tempos configuráveis de antecedência para eventos e bosses.
- Adicionada duração configurável para avisos de evento, boss e coleta.
- Adicionado botão para restaurar os padrões.
