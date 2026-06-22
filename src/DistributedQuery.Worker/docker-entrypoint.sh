#!/bin/sh
set -eu

if [ -d /data ]; then
    chown -R app:app /data
fi

exec runuser -u app -- dotnet /app/DistributedQuery.Worker.dll
