# Moovit Live Proxy - ParagensV2

Micro-serviço proxy autónomo que faz a ponte entre a aplicação **ParagensV2** e a telemetria GPS em tempo real do Moovit.

---

## Como Funciona

1. **Bypass do AWS WAF:** Utiliza o Playwright Chromium em modo headless para adquirir e manter atualizado o token criptográfico `aws-waf-token` emitido pelo CloudFront/Moovit.
2. **Consultas em Direto:** Executa a chamada oficial interna de telemetria `linearrival` dentro do contexto do browser e calcula a diferença de minutos e atraso real face ao horário programado.
3. **Servidor REST Local:** Expõe os dados em `http://localhost:5000/arrivals?stop={codigo_paragem}` para consumo instantâneo pela app.

---

## Como Iniciar

Basta dar duplo clique em:
```
start_proxy.bat
```
Ou no terminal:
```bash
python server.py
```

---

## Endpoints Disponíveis

* **Verificação de Saúde:**
  ```http
  GET http://localhost:5000/health
  ```
* **Chegadas em Tempo Real por Código de Paragem:**
  ```http
  GET http://localhost:5000/arrivals?stop=mai:621
  ```
  Exemplo de resposta:
  ```json
  {
    "stop": "mai:621",
    "arrivals": [
      {
        "line": "6305",
        "stopId": 36782842,
        "realtimeMinutes": 13,
        "delayMinutes": 5,
        "scheduledTime": "21:45",
        "isRealtime": true
      }
    ]
  }
  ```

---

## Utilização no Android

1. Se testares num dispositivo Android físico ligado por USB, executa uma única vez:
   ```bash
   adb reverse tcp:5000 tcp:5000
   ```
   Desta forma, a app no telemóvel comunica com `http://localhost:5000` através do cabo USB.
2. Se o proxy não estiver ligado, o **ParagensV2** usa de imediato os horários programados oficiais como fallback automático.
