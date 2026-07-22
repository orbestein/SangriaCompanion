# Sangria Companion v1.2.0.2 — HUD Startup Fix

Correções desta versão:

- remove a dependência do patch Harmony em `ClientChatSystem.OnUpdate`;
- localiza o World do cliente de forma segura durante o `Update`;
- garante que uma falha em patch opcional não impeça a HUD de ser criada;
- mantém o botão `SC` visível mesmo se a configuração `Enabled` estiver inconsistente;
- preserva tema Sangria, notificações, coleta, receitas e rastreamento móvel experimental.

Feche o V Rising antes de executar `COMPILAR_E_INSTALAR.bat`.
