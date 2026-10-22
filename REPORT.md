# Развёртывание приложения в Kubernetes

| Параметр | Значение |
|---|---|
| ФИО | Киселев Григорий Александрович |
| Группа | ФТ-402-2 |
| ОС | Windows |
| Архитектура компьютера | amd64 |
| Кластер | kind `hosting-lab`, контекст `kind-hosting-lab` |
| Registry | `localhost:5001` |
| Namespace | `homework-1` |
| Образ | `localhost:5001/kiselev-homework:v1` |

Приложение — HTTP-сервер на C# (.NET 10), слушает `0.0.0.0:8080`. На `GET /` возвращает сообщение из `APP_MESSAGE`, автора, окружение, признак наличия токена, имя пода и версию приложения. Deployment `hw1-app` запускает две реплики в namespace `homework-1`.

| Переменная | Источник | Назначение |
|---|---|---|
| `APP_MESSAGE` | ConfigMap `hw1-config` | Сообщение в HTTP-ответе |
| `APP_ENVIRONMENT` | ConfigMap `hw1-config` | Поле `environment`, значение `дз` |
| `APP_PORT` | ConfigMap `hw1-config` | Порт Kestrel, `8080` |
| `APP_TOKEN` | Secret `hw1-secret` | Проверка наличия токена (`tokenConfigured`) |

## Блок сборки и развёртывания

Команды выполняются из корня проекта.

```
kubectl config use-context kind-hosting-lab
docker build -t localhost:5001/kiselev-homework:v1 .
docker push localhost:5001/kiselev-homework:v1

kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/deployment.yaml
kubectl -n homework-1 rollout status deployment/hw1-app --timeout=180s
kubectl -n homework-1 get deployments,replicasets,pods -o wide
```

Результат push: [evidence/01-push.txt](evidence/01-push.txt).

```text
The push refers to repository [localhost:5001/kiselev-homework]
061d06e4000d: Preparing
9d0d9d3d1141: Preparing
53edabc8547a: Preparing
b77b1d83b00a: Preparing
1c41cda582c2: Preparing
4014493bfd8c: Preparing
42c74cbc219f: Preparing
32ab39660fd5: Preparing
42c74cbc219f: Waiting
32ab39660fd5: Waiting
4014493bfd8c: Waiting
9d0d9d3d1141: Layer already exists
1c41cda582c2: Layer already exists
53edabc8547a: Layer already exists
b77b1d83b00a: Layer already exists
32ab39660fd5: Layer already exists
42c74cbc219f: Layer already exists
4014493bfd8c: Layer already exists
061d06e4000d: Pushed
v1: digest: sha256:d5b274e78ce493aaaec7e1d941de9d9db615e6ce9add9834fa04148443dd0d9c size: 1995
```

В [evidence/02-infra.txt](evidence/02-infra.txt) Deployment имеет `READY 2/2`, ReplicaSet — две готовые реплики, оба пода находятся в состоянии `Running` с `RESTARTS 0`.

```text
NAME                      READY   UP-TO-DATE   AVAILABLE   AGE   CONTAINERS   IMAGES                               SELECTOR
deployment.apps/hw1-app   2/2     2            2           16s   hw1-app      localhost:5001/kiselev-homework:v1   app=hw1-app

NAME                                 DESIRED   CURRENT   READY   AGE   CONTAINERS   IMAGES                               SELECTOR
replicaset.apps/hw1-app-7c946b6cdf   2         2         2       16s   hw1-app      localhost:5001/kiselev-homework:v1   app=hw1-app,pod-template-hash=7c946b6cdf

NAME                           READY   STATUS    RESTARTS   AGE   IP           NODE                        NOMINATED NODE   READINESS GATES
pod/hw1-app-7c946b6cdf-65csl   1/1     Running   0          16s   10.244.0.9   hosting-lab-control-plane   <none>           <none>
pod/hw1-app-7c946b6cdf-trrtr   1/1     Running   0          16s   10.244.0.8   hosting-lab-control-plane   <none>           <none>
```

## Блок секретов

```
kubectl -n homework-1 logs -l app=hw1-app --prefix --tail=-1
kubectl -n homework-1 exec deployment/hw1-app -- sh -c 'test -n "$APP_TOKEN" && echo APP_TOKEN_is_set'
kubectl -n homework-1 get secret hw1-secret
```

В [evidence/03-check-token.txt](evidence/03-check-token.txt) у обоих подов есть строка `Starting v1 on port 8080: environment=дз, tokenConfigured=True`. Сохранённая проверка внутри контейнера вывела `APP_TOKEN is set` ([evidence/04-02-check-token.txt](evidence/04-02-check-token.txt)). Команда `get secret` показала тип `Opaque` и `DATA 1` ([evidence/04--token-exec.txt](evidence/04--token-exec.txt)). Эти проверки не выводят значение токена.

## Блок HTTP

В первом окне запустить и оставить работающим:

```
kubectl -n homework-1 port-forward deployment/hw1-app 8082:8080
```

Во втором окне выполнить:

```
curl.exe -i http://localhost:8082/
```

Ответ `HTTP/1.1 200 OK` сохранён в [evidence/07-exp2-before.txt](evidence/07-exp2-before.txt). В ответе указаны автор `Киселев Григорий Александрович`, окружение `дз`, `tokenConfigured: true` и версия `v1`.

```text
Секрет configmap
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Wed, 21 Oct 2026 17:20:27 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"message":"Секрет configmap","author":"Киселев Григорий Александрович","environment":"дз","tokenConfigured":true,"machineName":"hw1-app-7c946b6cdf-trrtr","appVersion":"v1"}
```

## Эксперимент 1: удаление пода

```
$COL = 'NAME:.metadata.name,UID:.metadata.uid,RESTARTS:.status.containerStatuses[0].restartCount'
kubectl -n homework-1 get pods -o "custom-columns=$COL"
$VICTIM = kubectl -n homework-1 get pods -l app=hw1-app -o jsonpath='{.items[0].metadata.name}'
kubectl -n homework-1 delete pod $VICTIM
kubectl -n homework-1 rollout status deployment/hw1-app --timeout=180s
kubectl -n homework-1 get pods -o "custom-columns=$COL"
kubectl -n homework-1 get pods -o 'custom-columns=NAME:.metadata.name,OWNER:.metadata.ownerReferences[0].kind,OWNER_NAME:.metadata.ownerReferences[0].name'
```

| Момент | Под | UID | RESTARTS |
|---|---|---|---|
| До | `hw1-app-7c946b6cdf-65csl` | `cf192de8-788d-4955-be6e-d4df93dc6d5d` | 0 |
| До | `hw1-app-7c946b6cdf-trrtr` | `ffb85a82-8f4f-4d0d-a4b6-84206c599280` | 0 |
| После | `hw1-app-7c946b6cdf-dgdst` | `d285b46a-ca44-4027-b28f-3a9961e43707` | 0 |
| После | `hw1-app-7c946b6cdf-trrtr` | `ffb85a82-8f4f-4d0d-a4b6-84206c599280` | 0 |

Исходное состояние: [evidence/06-exp1-before.txt](evidence/06-exp1-before.txt), удаление: [evidence/06-exp1-del.txt](evidence/06-exp1-del.txt), результат и владельцы подов: [evidence/06-exp1-after-del.txt](evidence/06-exp1-after-del.txt).

Фактические выводы до удаления, удаления и после восстановления реплик:

```text
NAME                       UID                                    RESTARTS
hw1-app-7c946b6cdf-65csl   cf192de8-788d-4955-be6e-d4df93dc6d5d   0
hw1-app-7c946b6cdf-trrtr   ffb85a82-8f4f-4d0d-a4b6-84206c599280   0
```

```text
pod "hw1-app-7c946b6cdf-65csl" deleted from homework-1 namespace
```

```text
NAME                       UID                                    RESTARTS
hw1-app-7c946b6cdf-dgdst   d285b46a-ca44-4027-b28f-3a9961e43707   0
hw1-app-7c946b6cdf-trrtr   ffb85a82-8f4f-4d0d-a4b6-84206c599280   0
NAME                       OWNER        OWNER_NAME
hw1-app-7c946b6cdf-dgdst   ReplicaSet   hw1-app-7c946b6cdf
hw1-app-7c946b6cdf-trrtr   ReplicaSet   hw1-app-7c946b6cdf
```

ReplicaSet `hw1-app-7c946b6cdf` восстановил число реплик, создав `dgdst` вместо удалённого `65csl`. Изменились имя и UID, а число рестартов осталось нулевым: создан новый под. При перезапуске контейнера внутри существующего пода имя и UID пода сохраняются, а счётчик рестартов увеличивается. Второй под остался прежним.

## Эксперимент 2: изменение ConfigMap

`APP_MESSAGE` изменено с `Секрет configmap` на `Секрет configmap другой` без пересборки образа. В текущем `k8s/configmap.yaml` уже сохранено итоговое значение. Для повторения эксперимента сначала вернуть исходное сообщение, применить ConfigMap и выполнить `rollout restart` с ожиданием `rollout status`, чтобы поды получили исходное значение.

В отдельном окне запустить:

```
kubectl -n homework-1 port-forward deployment/hw1-app 8082:8080
```

В основном окне проверить исходное состояние:

```
kubectl -n homework-1 get configmap hw1-config -o jsonpath='{.data.APP_MESSAGE}'
curl.exe -i http://localhost:8082/
```

В `k8s/configmap.yaml` заменить строку `APP_MESSAGE: "Секрет configmap"` на `APP_MESSAGE: "Секрет configmap другой"`, затем выполнить:

```
kubectl apply -f k8s/configmap.yaml
kubectl -n homework-1 get configmap hw1-config -o jsonpath='{.data.APP_MESSAGE}'
curl.exe -i http://localhost:8082/
```

ConfigMap уже содержит новое сообщение, а приложение отвечает старым. Остановить `port-forward` сочетанием Ctrl+C, запустить ту же команду заново и повторить HTTP-запрос: само переподключение не обновляет переменные окружения подов.

Далее перезапустить Deployment:

```
kubectl -n homework-1 rollout restart deployment/hw1-app
kubectl -n homework-1 rollout status deployment/hw1-app --timeout=180s
kubectl -n homework-1 get rs,pods
```

После завершения rollout остановить прежний `port-forward`, если он ещё работает, и снова запустить его в отдельном окне. Перенаправление привязано к выбранному поду и требует повторного запуска после его замены. Затем выполнить в основном окне:

```
curl.exe -i http://localhost:8082/
```

| Момент | В ConfigMap | В HTTP-ответе | Под, ответивший на запрос |
|---|---|---|---|
| До изменения | Секрет configmap | Секрет configmap | `hw1-app-7c946b6cdf-trrtr` |
| После apply, без перезапуска | Секрет configmap другой | Секрет configmap | `hw1-app-7c946b6cdf-trrtr` |
| После rollout restart | Секрет configmap другой | Секрет configmap другой | `hw1-app-d559cd945-zqm8c` |

Результаты: [до изменения](evidence/07-exp2-before.txt), [после apply](evidence/07-exp2-after-apply.txt), [ReplicaSet и поды после restart](evidence/07-exp2-after-restart.txt), [HTTP после restart](evidence/07-01-exp2-after-restart.txt).

После apply, до перезапуска:

```text
Секрет configmap другой
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Wed, 21 Oct 2026 17:22:23 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"message":"Секрет configmap","author":"Киселев Григорий Александрович","environment":"дз","tokenConfigured":true,"machineName":"hw1-app-7c946b6cdf-trrtr","appVersion":"v1"}
```

ReplicaSet и поды после rollout restart:

```text
NAME                                 DESIRED   CURRENT   READY   AGE
replicaset.apps/hw1-app-7c946b6cdf   0         0         0       11m
replicaset.apps/hw1-app-d559cd945    2         2         2       28s

NAME                          READY   STATUS    RESTARTS   AGE
pod/hw1-app-d559cd945-w7gfz   1/1     Running   0          27s
pod/hw1-app-d559cd945-zqm8c   1/1     Running   0          28s
```

HTTP-ответ после rollout restart и повторного запуска port-forward:

```text
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Wed, 21 Oct 2026 17:25:18 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"message":"Секрет configmap другой","author":"Киселев Григорий Александрович","environment":"дз","tokenConfigured":true,"machineName":"hw1-app-d559cd945-zqm8c","appVersion":"v1"}
```

Переменные окружения из ConfigMap задаются при создании контейнера. Изменение ConfigMap не меняет окружение работающих контейнеров и шаблон пода Deployment, поэтому автоматический rollout не начинается. Команда `rollout restart` меняет шаблон пода и запускает создание нового ReplicaSet. Новые поды получают обновлённые настройки. В сохранённом результате у старого ReplicaSet `hw1-app-7c946b6cdf` осталось ноль реплик, у нового `hw1-app-d559cd945` — две. Версия приложения осталась `v1`.

## Ресурсы

Значения из `k8s/deployment.yaml` заданы для каждого контейнера:

| Ресурс | requests | limits |
|---|---|---|
| CPU | 100m | 500m |
| RAM | 64Mi | 256Mi |
| ephemeral-storage | 64Mi | 256Mi |

При двух репликах суммарные requests приложения составляют `200m` CPU, `128Mi` памяти и `128Mi` временного хранилища. Замеры фактического потребления в сохранённых материалах отсутствуют.

## Ответы на вопросы

**Зачем нужны labels и selector?** Labels — метки объектов в виде пар «ключ/значение», selector — правило отбора по ним. В этом проекте `selector.matchLabels` Deployment совпадает с меткой `app=hw1-app` в шаблоне пода. ReplicaSet использует селектор для выбора подов, которыми управляет.

**Чем requests отличаются от limits?** Requests учитываются планировщиком при выборе узла по доступной для размещения ёмкости. Limits ограничивают потребление: для CPU применяется throttling, при нехватке памяти из-за лимита возможен OOM kill, а превышение лимита ephemeral-storage может привести к eviction пода.

**Чем ConfigMap отличается от Secret?** ConfigMap хранит несекретные настройки, Secret предназначен для токенов, паролей и ключей. Оба можно подключать через переменные окружения или файлы тома. Доступ к Secret регулируется RBAC. Обычный `kubectl get secret` без дополнительных форматов показывает метаданные и число ключей; пользователь с правами чтения может получить значения через API, например с `-o yaml`. Тип Secret сам по себе не гарантирует шифрования при хранении.

**Почему base64 не защищает пароль?** Это обратимое кодирование без ключа, поэтому значение можно декодировать. В `k8s/secret.yaml` используется `stringData`, где учебный токен записан обычным текстом.

## Удаление ресурсов после работы

```
kubectl delete -f k8s/deployment.yaml -f k8s/configmap.yaml -f k8s/secret.yaml --ignore-not-found
```

Команда удаляет Deployment домашней работы (его ReplicaSet и поды удаляются каскадно), ConfigMap и Secret из манифестов проекта. Namespace `homework-1`, кластер kind и локальный Registry сохраняются для следующего занятия.



