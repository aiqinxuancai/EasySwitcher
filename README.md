# EasySwitcher

EasySwitcher 是一个轻量的 API 转发与负载均衡服务，支持随机、轮询、权重与故障转移。它在转发时仅替换 API Key 与 Host，保留其他请求参数，并支持流式响应。所有调用都会输出详细日志，便于排查问题。

## 功能特性

- 负载均衡策略：`round_robin`、`random`、`weighted`、`failover`
- 支持分组配置与覆盖策略（超时、故障转移次数等）
- 健康检测与冷却时间，支持可重试状态码范围
- 流式转发（响应实时透传）
- 认证使用 `Authorization: Bearer`
- 控制台日志包含时间、分组、平台、状态码、耗时等
- 支持 Docker 与 docker-compose 部署

## 快速开始

1. 修改 `config.toml`（或直接使用 `examples/` 里的示例）。
2. 本地运行：

```bash
dotnet run --project EasySwitcher/EasySwitcher.csproj -- --config config.toml
```

3. 发送请求：

```bash
curl http://localhost:7085/v1/chat/completions \
  -H "Authorization: Bearer change-me" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"hello"}]}'
```

## 流式示例

```bash
curl -N http://localhost:7085/v1/chat/completions \
  -H "Authorization: Bearer change-me" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o-mini","stream":true,"messages":[{"role":"user","content":"hello"}]}'
```

说明：如果请求体长度未知（chunked transfer），EasySwitcher 会直接流式转发到上游，该请求不执行故障转移重试。

## 身份认证

使用：

```bash
Authorization: Bearer <server.auth_key>
```

## 分组路由

通过路径前缀指定分组，格式为：

```
http://<host>/{GROUP}/v1/...
```

当 `{GROUP}` 与已配置分组名称匹配时，将使用该分组，并在转发到上游时移除该路径段。

## 配置文件位置

也可以使用环境变量指定配置：

```bash
EASYSWITCHER_CONFIG=/path/to/config.toml
```

## 配置参考

顶层字段：

- `server.listen`: 监听地址，如 `http://0.0.0.0:7085`
- `server.auth_key`: 必填，用于外部调用认证
- `server.default_group`: 默认分组
- `server.strategy`: 默认负载均衡策略
- `server.timeout_seconds`: 上游请求超时（秒），默认 600
- `server.max_failover`: 最大尝试次数（包含首次）
- `server.max_request_body_bytes`: 可重试请求体的最大缓冲大小
- `health.failure_threshold`: 失败次数达到后标记为不健康
- `health.cooldown_seconds`: 冷却时间（秒）
- `health.retryable_status_min/max`: 可重试状态码范围（默认 500-599）
- `health.retry_on_429`: 是否将 429 视为可重试
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

- `round_robin`: 轮询
- `random`: 随机
- `weighted`: 按权重随机选主，再按优先级/权重排序
- `failover`: 只按优先级/权重排序

## Docker

构建并运行：

```bash
docker build -t easyswitcher .
docker run --rm -p 7085:7085 -v "$PWD/config.toml:/app/config.toml:ro" -e EASYSWITCHER_CONFIG=/app/config.toml easyswitcher
```

或使用 docker-compose：

```bash
docker-compose up -d --build
```

## 示例配置

- `examples/config.single-group.toml`: 单分组 + 轮询
- `examples/config.multi-group.toml`: 多分组 + 不同策略

## GitHub Actions

`/.github/workflows/release.yml` 会在推送 `v1.0.0` 这类 tag 时构建多平台发布包，并推送 Docker 镜像到 GHCR。
