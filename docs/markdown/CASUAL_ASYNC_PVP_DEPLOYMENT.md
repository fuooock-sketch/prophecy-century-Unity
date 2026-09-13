# 休闲异步 PVP 服务部署与联网单机验收

本服务只交换历史阵容快照。战斗、掉血、奖励和存档都由客户端本地执行；服务不可用时客户端会改用本地同回合候选池。因此部署目标是提高镜像池覆盖率，不是建立实时联机房间。

## 当前开发决定

开发阶段先使用开发电脑上的本地服务，不购买云服务器，也不配置公网域名。Unity 默认连接 `http://127.0.0.1:8765`；服务关闭时游戏仍会自动使用本地候选池完成回合。

直接双击项目根目录的 `启动休闲PVP本地服务.bat` 即可启动或重启本地服务。它会关闭占用 8765 端口的旧休闲 PVP 服务，再启动新的服务窗口。若端口被其他程序占用，它会停止并提示，不会误关该程序。

也可在项目根目录执行以下命令，并保持该 PowerShell 窗口开启：

```powershell
powershell -ExecutionPolicy Bypass -File tools/start_casual_pvp_local.ps1
```

浏览器打开 `http://127.0.0.1:8765/health`，返回 `"ok":true` 即表示服务已启动。开发完成、需要让其他设备共享历史阵容时，再按下面的正式部署流程迁移到云服务器。

调试局使用 `G` 加钱或其他 GM 属性修改后，会标记为调试局，不会上传或写入镜像池。若要清理已经在当天写入的本地镜像缓存，可双击项目根目录的 `清理今日休闲PVP镜像缓存.bat`；脚本会先在同目录生成备份，再删除当天 UTC 日期的缓存记录。

## 部署包

容器定义在 `tools/casual_pvp_server/Dockerfile`，Compose 文件在 `tools/casual_pvp_server/docker-compose.yml`。镜像只包含服务代码和当前单位/棋盘数据；SQLite 数据库挂载为命名卷 `casual_pvp_data`，容器重建不会清空镜像池。

服务端口默认只绑定目标机器的 `127.0.0.1`。生产环境应由同机 HTTPS 反向代理暴露 `/health` 与 `/api/v1/`；不要直接把 Python 服务端口公开到互联网。

```powershell
Copy-Item tools/casual_pvp_server/.env.example tools/casual_pvp_server/.env
docker compose --env-file tools/casual_pvp_server/.env -f tools/casual_pvp_server/docker-compose.yml up -d --build
docker compose -f tools/casual_pvp_server/docker-compose.yml ps
Invoke-RestMethod http://127.0.0.1:8765/health
```

发布客户端前，把 `Assets/Resources/Config/casual_pvp_network.json` 的 `defaultEndpoint` 改为部署后的 HTTPS 地址，例如 `https://pvp.example.com`。空值或非法地址会安全地回退到本机开发地址。已有用户可通过 `ProphecyCentury.CasualPvp.Endpoint` 的 PlayerPrefs 覆盖此值，便于灰度或本地调试。

## 备份与恢复

在线备份使用 SQLite backup API，先后校验源库和备份库的完整性，并输出 SHA-256：

```powershell
python tools/casual_pvp_server/backup_database.py `
  --database tools/casual_pvp_server/data/casual_pvp.db `
  --output-dir backups/casual_pvp
```

恢复会替换目标数据库，必须显式确认。先停掉服务，再执行恢复，再启动服务：

```powershell
python tools/casual_pvp_server/backup_database.py `
  --database tools/casual_pvp_server/data/casual_pvp.db `
  --restore backups/casual_pvp/casual_pvp_YYYYMMDDTHHMMSSZ.sqlite3 `
  --confirm-restore
```

建议每天一次备份并保留至少 14 天；每次游戏版本、战斗协议或单位数据更新前都做一次额外备份。观察 `/api/v1/pool/stats` 中每个版本/回合桶的 `poolSize`、`fallbackRate` 和 `duplicateRate`，用于确认镜像供给和本地回退比例。

## 联网单机验收

1. 在 HTTPS 域名上请求 `/health`，确认返回 `ok: true`。
2. 用两个不同存档在同一回合锁定阵容。第一个存档可以因池为空走本地回退；第二个存档应能取得对方上传的 `sourceType: player` 镜像。
3. 开始战斗并确认胜负、生命、奖励、存档均由本地正常更新；关闭网络后重复一次，确认仍能使用系统或本地镜像完成回合。
4. 在战后检查 `/api/v1/pool/stats`：对应版本、战斗协议和回合的请求数增加；池为空的首次请求会计入 `fallbackRate`。
5. 重启容器，重复 `/health` 和配对检查，确认命名卷中的镜像池仍在。
6. 在客户端把 `defaultEndpoint` 暂改为无效地址后测试；应在两秒内回退到本地候选池，且不会卡住商店或战斗流程。

本地代码级回归可用以下命令执行：

```powershell
python -m unittest discover -s tools/casual_pvp_server -p "test_*.py"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate_casual_pvp.ps1
```
