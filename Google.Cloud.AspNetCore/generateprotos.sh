#!/bin/bash

declare -r PROTOBUF_VERSION=3.7.0

declare -r ROOT=$(git rev-parse --show-toplevel)
declare -r PACKAGES=$ROOT/packages
declare -r PROTOBUF_DIR=$PACKAGES/Google.Protobuf.Tools.$PROTOBUF_VERSION

if [ ! -d "$PROTOBUF_DIR" ]
then
  nuget install -o $PACKAGES Google.Protobuf.Tools -Version $PROTOBUF_VERSION
fi

declare -r PROTOC=$PROTOBUF_DIR/tools/windows_x64/protoc.exe

declare -r KMS_DIR=$ROOT/Google.Cloud.AspNetCore/Google.Cloud.AspNetCore.DataProtection.Kms
$PROTOC -I $KMS_DIR --csharp_out=$KMS_DIR $KMS_DIR/*.proto
