#!/usr/bin/env bash
# Convert a .NET / Npgsql connection string (ADO.NET style, semicolon-
# delimited keyword=value) to a libpq keyword=value connection string
# suitable for psql / pg_restore / pg_dump / any libpq-based tool.
#
# Usage:
#   scripts/dotnet-to-libpq-connstring.sh "Host=...;Port=...;..."
#   echo "Host=..." | scripts/dotnet-to-libpq-connstring.sh
#
# Design rules:
#   - Every Npgsql key whose semantics are supported by libpq is translated
#     to its libpq equivalent. Pure .NET / Npgsql client-only keys (connection
#     pooling, client-side timeouts, socket buffer tuning, Windows-only auth
#     flags, Npgsql-specific flags) that have no libpq analogue are dropped
#     with a stderr note.
#   - Unknown keys FAIL the script (non-zero exit). We never silently drop
#     a value we don't recognize.
#   - Values are single-quoted on output; embedded ' and \ are backslash-
#     escaped per libpq quoting rules.
#   - Search Path / Timezone become "-c" fragments merged into a single
#     libpq "options" keyword.
#
# Npgsql -> libpq key map (* = non-trivial value translation):
#   Host, Server                      -> host
#   Port                              -> port
#   Database, DB, Initial Catalog     -> dbname
#   Username, User Id, User Name, User -> user
#   Password, Pwd                     -> password
#   Passfile                          -> passfile
#   Application Name                  -> application_name
#   Timeout, Connection Timeout       -> connect_timeout
#   SSL Mode *                        -> sslmode    (VerifyCA  -> verify-ca,
#                                                    VerifyFull -> verify-full)
#   Trust Server Certificate          -> (dropped; semantics covered by sslmode=require)
#   SSL Certificate                   -> sslcert
#   SSL Key                           -> sslkey
#   SSL Password                      -> sslpassword
#   Root Certificate                  -> sslrootcert
#   Kerberos Service Name             -> krbsrvname
#   Channel Binding                   -> channel_binding (value lowercased)
#   Target Session Attributes *       -> target_session_attrs (PascalCase -> kebab-case)
#   Load Balance Hosts                -> load_balance_hosts
#   Client Encoding, Encoding         -> client_encoding
#   Options                           -> options (merged with derived -c fragments)
#   Search Path *                     -> options '-c search_path=VAL'
#   Timezone *                        -> options '-c TimeZone=VAL'
#   Keepalive *                       -> keepalives=1 + keepalives_idle=VAL  (seconds)
#   TCP Keepalive *                   -> keepalives=1/0
#   TCP Keepalive Time *              -> keepalives_idle (ms / 1000)
#   TCP Keepalive Interval *          -> keepalives_interval (ms / 1000)
#
# Dropped (.NET/Npgsql-only, no libpq equivalent):
#   Pooling, Min/Minimum Pool Size, Max/Maximum Pool Size,
#   Connection Idle Lifetime, Connection Pruning Interval, Connection Lifetime,
#   Connection Reset, No Reset On Close,
#   Command Timeout, Internal Command Timeout, Cancellation Timeout,
#   Include Error Detail, Log Parameters,
#   Multiplexing, Load Table Composites, Load Types, Load Table Info,
#   Max Auto Prepare, Auto Prepare Min Usages,
#   Read Buffer Size, Write Buffer Size,
#   Socket Receive Buffer Size, Socket Send Buffer Size,
#   Integrated Security, Persist Security Info, Enlist,
#   Server Compatibility Mode, Convert Infinity Datetime,
#   Check Certificate Revocation, Include Realm,
#   Array Nullability Mode, Is Recording Statistics, Host Recheck Seconds,
#   Socket Path
#
# Limitations:
#   - Does not parse Npgsql's double-quoted-value form for values that
#     themselves contain ';' or '"'. If a real secret ever needs that,
#     extend the parser.

set -euo pipefail

INPUT="${1:-}"
if [[ -z "$INPUT" ]]; then
    INPUT=$(cat)
fi

to_lower() {
    printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
}

normalize_key() {
    # lowercase, drop spaces and underscores — so "User Id", "user_id",
    # "USERID" all match.
    printf '%s' "$1" | tr '[:upper:]' '[:lower:]' | tr -d ' _'
}

trim() {
    local v="$1"
    v="${v#"${v%%[![:space:]]*}"}"
    v="${v%"${v##*[![:space:]]}"}"
    printf '%s' "$v"
}

quote_value() {
    # libpq: wrap in single quotes, backslash-escape embedded ' and \.
    local v="$1"
    v="${v//\\/\\\\}"
    v="${v//\'/\\\'}"
    printf "'%s'" "$v"
}

# Parse an Npgsql boolean literal to 0/1 for libpq.
parse_bool() {
    local v
    v=$(to_lower "$(trim "$1")")
    case "$v" in
        true|1|yes|on)   printf '1' ;;
        false|0|no|off)  printf '0' ;;
        *)
            echo "Error: boolean value expected, got '$1'" >&2
            return 1
            ;;
    esac
}

# Milliseconds -> seconds (truncated integer division).
ms_to_seconds() {
    local ms
    ms=$(trim "$1")
    if ! [[ "$ms" =~ ^-?[0-9]+$ ]]; then
        echo "Error: integer milliseconds value expected, got '$1'" >&2
        return 1
    fi
    printf '%d' $(( ms / 1000 ))
}

OUT=""
# Accumulated startup options. libpq's "options" keyword takes a single
# shell-ish string of "-c key=val" fragments. We append fragments here and
# emit one merged options=... at the end (merging any passthrough Options=
# the user provided directly).
OPTIONS_RAW=""

append_opt() {
    if [[ -n "$OPTIONS_RAW" ]]; then
        OPTIONS_RAW+=" $1"
    else
        OPTIONS_RAW="$1"
    fi
}

emit() {
    OUT+="$1=$(quote_value "$2") "
}

# Npgsql Target Session Attributes values use PascalCase; libpq uses
# kebab-case lowercase. Map known values; unknowns pass through lowercased.
translate_target_session_attrs() {
    local v
    v=$(to_lower "$(trim "$1")")
    case "$v" in
        any|primary|standby)                    printf '%s' "$v" ;;
        readwrite|read-write)                   printf 'read-write' ;;
        readonly|read-only)                     printf 'read-only' ;;
        preferstandby|prefer-standby)           printf 'prefer-standby' ;;
        preferprimary|prefer-primary)           printf 'prefer-standby' ;;  # libpq has no prefer-primary; fall back
        *)                                      printf '%s' "$v" ;;
    esac
}

IFS=';' read -ra PAIRS <<< "$INPUT"
for PAIR in "${PAIRS[@]}"; do
    PAIR=$(trim "$PAIR")
    [[ -z "$PAIR" ]] && continue

    if [[ "$PAIR" != *"="* ]]; then
        echo "Error: malformed pair (missing '='): $PAIR" >&2
        exit 1
    fi

    KEY="${PAIR%%=*}"
    VALUE="${PAIR#*=}"
    KEY=$(trim "$KEY")
    VALUE=$(trim "$VALUE")
    KEY_NORM=$(normalize_key "$KEY")

    case "$KEY_NORM" in
        # --- Core ---
        host|server)                        emit host "$VALUE" ;;
        port)                               emit port "$VALUE" ;;
        user|userid|username)               emit user "$VALUE" ;;
        password|pwd)                       emit password "$VALUE" ;;
        database|db|initialcatalog)         emit dbname "$VALUE" ;;
        passfile)                           emit passfile "$VALUE" ;;
        applicationname)                    emit application_name "$VALUE" ;;
        timeout|connectiontimeout|connecttimeout)
                                            emit connect_timeout "$VALUE" ;;

        # --- SSL / auth ---
        sslmode)
            V_LOWER=$(to_lower "$VALUE")
            case "$V_LOWER" in
                verifyca)   V_LIBPQ="verify-ca" ;;
                verifyfull) V_LIBPQ="verify-full" ;;
                *)          V_LIBPQ="$V_LOWER" ;;
            esac
            emit sslmode "$V_LIBPQ"
            ;;
        trustservercertificate)
            echo "Info: dropped .NET-only key 'Trust Server Certificate' (semantics already covered by sslmode)." >&2
            ;;
        sslcertificate)                     emit sslcert "$VALUE" ;;
        sslkey)                             emit sslkey "$VALUE" ;;
        sslpassword)                        emit sslpassword "$VALUE" ;;
        rootcertificate)                    emit sslrootcert "$VALUE" ;;
        kerberosservicename)                emit krbsrvname "$VALUE" ;;
        channelbinding)                     emit channel_binding "$(to_lower "$VALUE")" ;;

        # --- Routing ---
        targetsessionattributes)
            emit target_session_attrs "$(translate_target_session_attrs "$VALUE")"
            ;;
        loadbalancehosts)                   emit load_balance_hosts "$(parse_bool "$VALUE")" ;;

        # --- Session init (merged into libpq "options") ---
        clientencoding|encoding)            emit client_encoding "$VALUE" ;;
        options)
            # Passthrough of existing -c fragments.
            append_opt "$VALUE"
            ;;
        searchpath)                         append_opt "-c search_path=$VALUE" ;;
        timezone)                           append_opt "-c TimeZone=$VALUE" ;;

        # --- TCP keepalives ---
        # Npgsql "Keepalive": TCP keepalive time in SECONDS (0 = disabled).
        keepalive)
            if [[ "$VALUE" == "0" ]]; then
                emit keepalives "0"
            else
                emit keepalives "1"
                emit keepalives_idle "$VALUE"
            fi
            ;;
        tcpkeepalive)                       emit keepalives "$(parse_bool "$VALUE")" ;;
        # Npgsql "TCP Keepalive Time" / "TCP Keepalive Interval" are in
        # MILLISECONDS; libpq keepalives_idle / keepalives_interval are in
        # SECONDS. Truncate on conversion.
        tcpkeepalivetime)                   emit keepalives_idle "$(ms_to_seconds "$VALUE")" ;;
        tcpkeepaliveinterval)               emit keepalives_interval "$(ms_to_seconds "$VALUE")" ;;

        # --- .NET / Npgsql-only: no libpq analogue ---
        pooling|minpoolsize|minimumpoolsize|maxpoolsize|maximumpoolsize|\
        connectionidlelifetime|connectionpruninginterval|connectionlifetime|\
        connectionreset|noresetonclose|\
        commandtimeout|internalcommandtimeout|cancellationtimeout|\
        includeerrordetail|logparameters|\
        multiplexing|loadtablecomposites|loadtypes|loadtableinfo|\
        maxautoprepare|autoprepareminusages|\
        readbuffersize|writebuffersize|\
        socketreceivebuffersize|socketsendbuffersize|\
        integratedsecurity|persistsecurityinfo|enlist|\
        servercompatibilitymode|convertinfinitydatetime|\
        checkcertificaterevocation|includerealm|\
        arraynullabilitymode|isrecordingstatistics|hostrecheckseconds|\
        socketpath|maintenanceuser|eftemplatedatabase|efadmindatabase|\
        serversni|nosynchronoustransaction)
            echo "Info: dropped .NET/Npgsql-only key '$KEY' (no libpq equivalent)." >&2
            ;;

        *)
            echo "Error: unknown connection string key '$KEY'. Refuse to drop silently — add explicit handling in scripts/dotnet-to-libpq-connstring.sh." >&2
            exit 1
            ;;
    esac
done

# Emit the merged options keyword if we accumulated any fragments.
if [[ -n "$OPTIONS_RAW" ]]; then
    OUT+="options=$(quote_value "$OPTIONS_RAW") "
fi

OUT="${OUT% }"

if [[ "$OUT" != *"host="* ]] || [[ "$OUT" != *"dbname="* ]]; then
    echo "Error: converted connection string is missing host or dbname." >&2
    exit 1
fi

printf '%s\n' "$OUT"
