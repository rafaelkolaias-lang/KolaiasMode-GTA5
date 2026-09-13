# KolaiasMode - GTA V Mod

Mod para GTA V que combina **Speed Limiter** (limitador de velocidade) + **Drift Handling** (modo drift) em um unico toggle.

## Funcionalidades

### Speed Limiter
- Limita a velocidade maxima do veiculo
- Ao ativar, pega a velocidade atual como limite inicial
- Ajuste em tempo real com **Page Up / Page Down** (incremento de 5 km/h)
- Segurar **Shift** desativa o limite temporariamente (solta = volta a limitar)
- HUD no topo da tela mostra o limite e velocidade atual

### Drift Handling
- **Tracao** reduzida para 65% (pneus escorregam mais)
- **Aderencia** reduzida para 25% (pista parece molhada)
- **Freio** reduzido para 25% (freia muito menos)
- **Angulo de curva** reduzido para 50% (curvas mais abertas)
- Segurar **Shift** restaura o freio original temporariamente
- Aplica automaticamente em qualquer veiculo que voce entrar enquanto o mod estiver ativo

## Controles

| Tecla | Acao |
|---|---|
| **Ctrl + F11** | Ativa / Desativa o mod |
| **Page Up** | Aumenta limite de velocidade (+5 km/h) |
| **Page Down** | Diminui limite de velocidade (-5 km/h) |
| **Shift** (segurar) | Desativa limite de velocidade + restaura freio temporariamente |
| **Insert** | Recarrega o script sem reiniciar o jogo |

## Comportamento

- O mod **nao desativa ao sair do carro** - so desativa com Ctrl+F11
- Ao entrar em um novo carro com o mod ativo, o drift handling e aplicado automaticamente
- Ao iniciar o jogo, aparece uma mensagem: *"KolaiasMode loaded! Ctrl+F11 to toggle"*

## Requisitos

1. **ScriptHookV** (Alexander Blade) - [dev-c.com/gtav/scripthookv](http://www.dev-c.com/gtav/scripthookv/)
2. **ScriptHookVDotNet** v3.7.0+ (nightly) - [github.com/scripthookvdotnet/scripthookvdotnet-nightly](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases)

## Instalacao

1. Instale o **ScriptHookV**:
   - Extraia `ScriptHookV.dll`, `dinput8.dll` e `NativeTrainer.asi` na raiz do GTA V

2. Instale o **ScriptHookVDotNet nightly** (v3.7.0+):
   - Extraia `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll` e `ScriptHookVDotNet3.dll` na raiz do GTA V
   - **IMPORTANTE**: Use a versao **nightly**, nao a stable v3.6.0 (veja erros conhecidos abaixo)

3. Copie `ScriptHookVDotNet.ini` para a raiz do GTA V

4. Crie a pasta `scripts` na raiz do GTA V (se nao existir)

5. Copie `SpeedLimiter.cs` para a pasta `scripts`

### Estrutura de arquivos
```
Grand Theft Auto V/
├── dinput8.dll              (ScriptHookV)
├── ScriptHookV.dll          (ScriptHookV)
├── NativeTrainer.asi        (ScriptHookV)
├── ScriptHookVDotNet.asi    (SHVDN nightly)
├── ScriptHookVDotNet2.dll   (SHVDN nightly)
├── ScriptHookVDotNet3.dll   (SHVDN nightly)
├── ScriptHookVDotNet.ini    (config)
└── scripts/
    └── SpeedLimiter.cs      (este mod)
```

## Erros conhecidos e solucoes

### 1. `using GTA.UI` - namespace nao existe
**Erro**: `Uma diretiva de namespace using so pode ser aplicada a namespaces. 'GTA.UI' e um tipo, nao um namespace`

**Causa**: `GTA.UI` e uma classe, nao um namespace no SHVDN v2/v3. Usar `Notification.Show()` nao funciona.

**Solucao**: Usar natives diretas para notificacoes:
```csharp
// Em vez de: Notification.Show("texto");
Function.Call((Hash)0x202709F4C58A0424, "STRING");
Function.Call((Hash)0x6C188BE134E074AA, "texto");
Function.Call((Hash)0x2ED7843F8F801023, false, false);
```

### 2. ScriptHookVDotNet v3.6.0 (stable) - crash fatal `NativeMemory`
**Erro**: `System.TypeInitializationException: O inicializador de tipo de 'SHVDN.NativeMemory' acionou uma excecao`

**Causa**: A versao stable v3.6.0 do SHVDN nao e compativel com builds recentes do GTA V (ex: build 3889.0).

**Solucao**: Usar a versao **nightly** (v3.7.0+) do ScriptHookVDotNet:
```
https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases
```

### 3. `GET_VEHICLE_HANDLING_FLOAT` - native nao existe
**Erro**: `SCRIPT HOOK V CRITICAL ERROR - FATAL: Can't find native 0x642FC12F36B5E6DE`

**Causa**: As natives `GET_VEHICLE_HANDLING_FLOAT` (0x642FC12F36B5E6DE) e `SET_VEHICLE_HANDLING_FLOAT` (0x488C86D2B0C71BD1) nao existem em todas as builds do GTA V.

**Solucao**: Usar a API do SHVDN `veh.HandlingData` em vez de natives:
```csharp
// Em vez de: Function.Call(Hash.GET_VEHICLE_HANDLING_FLOAT, veh, "CHandlingData", "fBrakeForce");
var hd = veh.HandlingData;
float brake = hd.BrakeForce;
hd.BrakeForce = brake * 0.25f;
```

### 4. `DisableControlThisFrame` - numero errado de argumentos
**Erro**: Versoes diferentes do SHVDN esperam 1 ou 2 argumentos para `Game.DisableControlThisFrame()`.

**Solucao**: Usar a native direta que sempre aceita os mesmos parametros:
```csharp
// DISABLE_CONTROL_ACTION(group, control, disable)
Function.Call((Hash)0xFE99B66D079CF6BC, 0, 71, true);
```

### 5. `ConsoleKey=F4` - fecha o jogo
**Erro**: Pressionar F4 para abrir o console do SHVDN fecha o jogo (Alt+F4 / conflito).

**Solucao**: Desativar o console no `ScriptHookVDotNet.ini`:
```ini
ReloadKey=Insert
ConsoleKey=None
```

### 6. Script tenta exibir texto antes do jogo carregar
**Erro**: Crash ao chamar natives de texto no menu principal (antes de entrar no Story Mode).

**Solucao**: Verificar se o jogador esta no mundo antes de chamar qualquer native:
```csharp
if (Game.Player == null || Game.Player.Character == null)
    return;
if (!startupShown && Game.Player.CanControlCharacter)
{
    startupShown = true;
    ShowSubtitle("texto", 8000);
}
```

## Valores do Drift Handling

| Parametro | Multiplicador | Efeito |
|---|---|---|
| TractionCurveMin | 0.65x | 35% menos tracao |
| TractionCurveMax | 0.65x | 35% menos tracao |
| TractionLossMultiplier | 0.25x | 75% menos aderencia |
| BrakeForce | 0.25x | 75% menos freio |
| SteeringLock | 0.5x | 50% menos angulo de curva |

## Licenca

MIT License - use e modifique livremente.
