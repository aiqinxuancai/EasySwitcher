# AviSwitch

AviSwitch 是一个轻量的 API 转发与负载均衡服务，支持加权轮询与主备故障转移。它在转发时仅替换 API Key 与 Host，保留其他请求参数，并支持流式响应，

特别适合各个Claude、Codex分发平台经常爆炸的问题，让你可以在"鸡鸭鹅"之间API全自动切换重试，避免各平台爆炸后用cc-switch配置切换文件后要退出重开工具恢复会话的尴尬场景，保证一直蹬着不断（尽量）。

<img width="1024" height="913" alt="image" src="https://github.com/user-attachments/assets/43851fe9-ce44-4b04-a7ad-962a165d0ecd" />



## 功能特性

- 负载均衡策略：`weighted(加权轮询)`、`failover(主备机制)`  
- 支持分组配置与覆盖策略（超时、熔断阈值等）
- 健康检测与冷却时间，HTTP错误触发熔断，连续触发时冷却倍增
- 流式转发（响应实时透传）
- 控制台日志包含时间、分组、平台、状态码、耗时等
- 支持 Docker 与 docker-compose 部署
<img width="2528" height="1696" alt="image" src="https://github.com/user-attachments/assets/1a3e95b9-ea37-429e-9a98-7a07fc52e5f5" />


## 快速开始

1. 修改 `config.toml`（参考下方示例）。
2. 本地运行：

```bash
AviSwitch.exe --config config.toml
```

注：也可用环境变量指定配置文件`AVISWITCH_CONFIG=/path/to/config.toml`

### 使用Docker部署

**我更推荐使用Docker部署在你的NAS等设备上，通过Tailscale等组网工具用内部地址来调用。**

构建并运行：

```bash

docker run -d \
  --name aviswitch \
  --restart unless-stopped \
  -p 7085:7085 \
  -e AVISWITCH_CONFIG=/app/config.toml \  
  -v $PWD/config.toml:/app/config.toml \
  ghcr.io/aiqinxuancai/aviswitch:latest

```

或使用 docker-compose：

```yml
services:
  aviswitch:
    image: ghcr.io/aiqinxuancai/aviswitch:latest
    container_name: aviswitch
    restart: unless-stopped
    ports:
      - "7085:7085"
    volumes:
      - ./config.toml:/app/config.toml:ro
    environment:
      - AVISWITCH_CONFIG=/app/config.toml
```

## 配置到Codex、Claude、Gemini

Codex配置例子，只需将接口URL指定到AviSwitch，比如http://127.0.0.1:7085/
```
model_provider = "aviswitch"
model = "gpt-5.2-codex"
model_reasoning_effort = "high"
disable_response_storage = true
sandbox_mode="danger-full-access"

[model_providers.aviswitch]
name = "AviSwitch"
base_url = "http://100.100.1.7:7085/"
wire_api = "responses"
requires_openai_auth = true
```

Claude配置同理，只需改ANTHROPIC_BASE_URL即可，我这里是用了一个claude的分组
```json
{
  "env": {
    "ANTHROPIC_AUTH_TOKEN": "your_api_key_here",
    "ANTHROPIC_BASE_URL": "http://127.0.0.1:7085/claude",
    "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1"
  },
  "permissions": {
    "allow": [],
    "deny": []
  }
}
```

**分组路由**

通过路径前缀指定分组，格式为：

```
http://<host>/{GROUP}/
```

分组配置请看下面例子，当 `{GROUP}` 与已配置分组名称匹配时，将使用该分组，并在转发到上游时移除该路径段。

## 配置参考

顶层字段：

- `server.listen`: 监听地址，如 `http://0.0.0.0:7085`
- `server.auth_key`: 必填，用于外部调用认证
- `server.default_group`: 默认分组
- `server.strategy`: 默认负载均衡策略（weighted 或 failover）
- `server.timeout_seconds`: 上游请求超时（秒），默认 600
- `server.max_failover`: 触发熔断冷却的连续失败次数
- `server.max_request_body_bytes`: 可重试请求体的最大缓冲大小
- `health.cooldown_seconds`: 冷却时间（秒，基础冷却，连续熔断按倍数增加）
- `groups.<name>`: 分组覆盖配置（策略/熔断阈值/超时）
- `[[platforms]]`: 上游平台列表

重试说明：当请求体可重试时，失败会按负载均衡顺序依次切换到后续候选节点，直到候选耗尽。

平台字段：

- `name`: 用于日志显示的名称
- `base_url`: 上游基础地址
- `api_key`: 上游 API Key
- `group`: 分组名
- `weight`: 权重（越大越容易被选中）
- `priority`: 优先级（越小越优先，故障转移使用）
- `key_type`: 预定义类型，支持`openai`/`claude`/`gemini`，会自动设置 `key_header`/`key_prefix`
- `key_header`: 自定义 API Key 的请求头名称，在未设置 `key_type` 时使用
- `key_prefix`: API Key 前缀（如 `Bearer `），在未设置 `key_type` 时使用
- `key_type`与`key_header`/`key_prefix` 两种方式二选一即可
- `enabled`: 是否启用

策略说明：

- `weighted`: 加权轮询（按权重分配请求）
- `failover`: 主备机制（同优先级按权重轮询，优先级更高的节点不可用时才切换）

## 示例配置

**建议直接拿例子改，就复制复制，然后改改base_url和key以及name就够了**

### 单分组（主备模式）

例子里88code为主，鹅cubence为备用，在88code炸了后使用鹅cubence，如果88code恢复后自动切回，你也可以多个备用。

```toml
[server]
listen = "http://0.0.0.0:7085"
auth_key = "change-me"
default_group = "default"

[groups.default]
strategy = "failover"
max_failover = 2
timeout_seconds = 600

[[platforms]]
name = "88code"
base_url = "https://www.88code.ai/openai/v1"
api_key = ""
group = "default"
weight = 1
priority = 0
key_type = "openai"
enabled = true

[[platforms]]
name = "鹅cubence"
base_url = "https://api.cubence.com/v1"
api_key = ""
group = "default"
weight = 1
priority = 1
key_type = "openai"
enabled = true

```


### 单分组（加权轮询）

循环使用下面的4个平台。

```toml
[server]
listen = "http://0.0.0.0:7085"
auth_key = "change-me"
default_group = "default"

[groups.default]
strategy = "weighted"
max_failover = 1
timeout_seconds = 600

[[platforms]]
name = "88code"
base_url = "https://www.88code.ai/openai/v1"
api_key = ""
group = "default"
weight = 1
priority = 0
key_type = "openai"
enabled = true

[[platforms]]
name = "鹅cubence"
base_url = "https://api.cubence.com/v1"
api_key = ""
group = "default"
weight = 1
priority = 0
key_type = "openai"
enabled = true

[[platforms]]
name = "Privnode"
base_url = "https://privnode.com/v1"
api_key = ""
group = "default"
weight = 1
priority = 1
key_type = "openai"
enabled = false

[[platforms]]
name = "鸭Duckcoding"
base_url = "https://jp.duckcoding.com/v1"
api_key = ""
group = "default"
weight = 1
priority = 1
key_type = "openai"
enabled = true
```

### 多分组

适用于`CodeX`和`CC`都用的人，默认组为`CodeX`，`claude`组为ClaudeCode接口

```toml
[server]
listen = "http://0.0.0.0:7085"
auth_key = ""
default_group = "default"
strategy = "weighted"
timeout_seconds = 600
max_failover = 1
max_request_body_bytes = 10485760

[health]
cooldown_seconds = 60

[groups.default]
strategy = "failover"
max_failover = 1
timeout_seconds = 600

# 下面是默认的CodeX分组，使用 http://127.0.0.1:7085/ 或 http://127.0.0.1:7085/default 作为CodeX的配置入口

[[platforms]]
name = "88"
base_url = "https://www.88code.ai/openai/v1"
api_key = "你的平台Key"
group = "default"
weight = 1
priority = 0
key_type = "openai"
enabled = true

[[platforms]]
name = "鹅cubence"
base_url = "https://api.cubence.com/v1"
api_key = "你的平台Key"
group = "default"
weight = 1
priority = 0
key_type = "openai"
enabled = true

[[platforms]]
name = "Privnode"
base_url = "https://privnode.com/v1"
api_key = "你的平台Key"
group = "default"
weight = 1
priority = 1
key_type = "openai"
enabled = true

[[platforms]]
name = "Duckcoding"
base_url = "https://jp.duckcoding.com/v1"
api_key = "你的平台Key"
group = "default"
weight = 1
priority = 1
key_type = "openai"
enabled = true

# 下面是claude分组，使用 http://127.0.0.1:7085/claude 作为Claude Code的配置入口

[[platforms]]
name = "88"
base_url = "https://www.88code.ai/api/"
api_key = "你的平台Key"
group = "claude"
weight = 1
priority = 0
key_type = "claude"
enabled = true

[[platforms]]
name = "鹅cubence"
base_url = "https://api.cubence.com/v1"
api_key = "你的平台Key"
group = "claude"
weight = 1
priority = 0
key_type = "claude"
enabled = true

[[platforms]]
name = "Privnode"
base_url = "https://privnode.com/v1"
api_key = "你的平台Key"
group = "claude"
weight = 1
priority = 1
key_type = "claude"
enabled = true

[[platforms]]
name = "Duckcoding"
base_url = "https://jp.duckcoding.com/v1"
api_key = "你的平台Key"
group = "claude"
weight = 1
priority = 1
key_type = "claude"
enabled = true

```

### 平台认证填写示例

```toml
[[platforms]]
# OpenAI：Authorization + Bearer 前缀
name = "openai"
base_url = "https://api.openai.com"
api_key = "sk-..."
key_type = "openai"

[[platforms]]
# Gemini：X-Goog-Api-Key，无前缀
name = "gemini"
base_url = "https://generativelanguage.googleapis.com"
api_key = "..."
key_type = "gemini"

[[platforms]]
# Claude：x-api-key，无前缀
name = "claude"
base_url = "https://api.anthropic.com"
api_key = "sk-ant-..."
key_type = "claude"
```

