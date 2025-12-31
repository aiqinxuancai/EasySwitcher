# EasySwitcher

EasySwitcher 是一个轻量的 API 转发与负载均衡服务，支持加权轮询与主备故障转移。它在转发时仅替换 API Key 与 Host，保留其他请求参数，并支持流式响应。

## 功能特性

- 负载均衡策略：`weighted`、`failover`
- 支持分组配置与覆盖策略（超时、故障转移次数等）
- 健康检测与冷却时间，HTTP错误触发熔断，连续触发时冷却倍增
- 流式转发（响应实时透传）
- 控制台日志包含时间、分组、平台、状态码、耗时等
- 支持 Docker 与 docker-compose 部署

## 快速开始

1. 修改 `config.toml`（参考 `examples/` 里的示例）。
2. 本地运行：

```bash
EasySwitcher.exe --config config.toml
```

注：也可用环境变量指定配置文件`EASYSWITCHER_CONFIG=/path/to/config.toml`

### 使用Docker部署

构建并运行：

```bash

docker run -d \
  --name easyswitcher \
  --restart unless-stopped \
  -p 7085:7085 \
  -e EASYSWITCHER_CONFIG=/app/config.toml \  
  -v $PWD/config.toml:/app/config.toml \
  ghcr.io/aiqinxuancai/easyswitcher:latest

```

或使用 docker-compose：

```yml
services:
  easyswitcher:
    image: ghcr.io/aiqinxuancai/easyswitcher:latest
    container_name: easyswitcher
    restart: unless-stopped
    ports:
      - "7085:7085"
    volumes:
      - ./config.toml:/app/config.toml:ro
    environment:
      - EASYSWITCHER_CONFIG=/app/config.toml
```

## 分组路由

通过路径前缀指定分组，格式为：

```
http://<host>/{GROUP}/v1/...
```

当 `{GROUP}` 与已配置分组名称匹配时，将使用该分组，并在转发到上游时移除该路径段。

## 配置参考

顶层字段：

- `server.listen`: 监听地址，如 `http://0.0.0.0:7085`
- `server.auth_key`: 必填，用于外部调用认证
- `server.default_group`: 默认分组
- `server.strategy`: 默认负载均衡策略（weighted 或 failover）
- `server.timeout_seconds`: 上游请求超时（秒），默认 600
- `server.max_failover`: 最大尝试次数（包含首次）
- `server.max_request_body_bytes`: 可重试请求体的最大缓冲大小
- `health.failure_threshold`: 400+ 错误或请求异常/超时次数达到后标记为不健康
- `health.cooldown_seconds`: 冷却时间（秒，基础冷却，连续熔断按倍数增加）
- `groups.<name>`: 分组覆盖配置（策略/重试次数/超时）
- `[[platforms]]`: 上游平台列表

平台字段：

- `name`: 用于日志显示的名称
- `base_url`: 上游基础地址
- `api_key`: 上游 API Key
- `group`: 分组名
- `weight`: 权重（越大越容易被选中）
- `priority`: 优先级（越小越优先，故障转移使用）
- `key_header`: 注入 API Key 的请求头
- `key_prefix`: API Key 前缀（如 `Bearer `）
- `enabled`: 是否启用

策略说明：

- `weighted`: 加权轮询（按权重分配请求）
- `failover`: 主备机制（同优先级按权重轮询，优先级更高的节点不可用时才切换）

## 示例配置

- `examples/config.single-group.toml`: 单分组 + 加权轮询
- `examples/config.multi-group.toml`: 多分组 + 不同策略

