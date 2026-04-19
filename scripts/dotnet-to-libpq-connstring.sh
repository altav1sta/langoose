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
#   (implicit) sslmode verify-{ca,full} with no Root Certificate
#                                     -> sslrootcert=system  (libpq 16+;
#                                        mirrors .NET's default OS trust store)
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
#   TCP Keepalive Time                -> keepalives_idle  (same unit: seconds)
#   TCP Keepalive Interval            -> keepalives_interval (same unit: seconds)
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

# Helpers below exit on validation failure. `set -e` does not propagate
# a non-zero return through `$(...)` used as a function argument (unlike
# bare assignment), so bare `return 1` would leak an empty value and
# continue. Using `exit` kills the whole script regardless of call site.

# Parse an Npgsql boolean literal to 0/1 for libpq.
parse_bool() {
    local v
    v=$(to_lower "$(trim "$1")")
    case "$v" in
        true|1|yes|on)   printf '1' ;;
        false|0|no|off)  printf '0' ;;
        *)
            echo "Error: boolean value expected, got '$1'" >&2
            exit 1
            ;;
    esac
}

require_integer_seconds() {
    local v
    v=$(trim "$1")
    if ! [[ "$v" =~ ^-?[0-9]+$ ]]; then
        echo "Error: integer seconds value expected, got '$1'" >&2
        exit 1
    fi
    printf '%s' "$v"
}

OUT=""
# Accumulated startup options. libpq's "options" keyword takes a single
# shell-ish string of "-c key=val" fragments. We append fragments here and
# emit one merged options=... at the end (merging any passthrough Options=
# the user provided directly).
OPTIONS_RAW=""
# Track sslmode and whether an explicit Root Certificate was provided, so
# that we can default sslrootcert=system for verify-{ca,full} when the
# input didn't specify one. .NET / Npgsql verifies against the OS trust
# store by default; libpq defaults to looking at ~/.postgresql/root.crt
# which doesn't exist in a typical pg_* container. sslrootcert=system
# (libpq 16+) matches .NET's behaviour and works with standard public-CA
# providers (Neon / Supabase / RDS / ...).
SSLMODE_FINAL=""
SSLROOTCERT_SET=0

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
# kebab-case lowercase. Only translate values that have an exact libpq
# equivalent. Anything else fails — silently falling back to "any" or
# inverting the preference (e.g. prefer-primary -> prefer-standby) would
# send pg_restore to the wrong node.
translate_target_session_attrs() {
    local v
    v=$(to_lower "$(trim "$1")")
    case "$v" in
        any|primary|standby)              printf '%s' "$v" ;;
        readwrite|read-write)             printf 'read-write' ;;
        readonly|read-only)               printf 'read-only' ;;
        preferstandby|prefer-standby)     printf 'prefer-standby' ;;
        preferprimary|prefer-primary)
            echo "Error: 'Target Session Attributes=PreferPrimary' has no libpq equivalent (libpq offers 'primary' strict-only or 'prefer-standby'; it does not have a prefer-primary mode). Change the secret to 'primary' (reject non-writers) or 'any' (no preference), or extend the converter with an explicit strategy." >&2
            exit 1
            ;;
        *)
            echo "Error: unsupported 'Target Session Attributes' value '$1'. Add an explicit mapping in scripts/dotnet-to-libpq-connstring.sh." >&2
            exit 1
            ;;
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
            SSLMODE_FINAL="$V_LIBPQ"
            emit sslmode "$V_LIBPQ"
            ;;
        trustservercertificate)
            echo "Info: dropped .NET-only key 'Trust Server Certificate' (semantics already covered by sslmode)." >&2
            ;;
        sslcertificate)                     emit sslcert "$VALUE" ;;
        sslkey)                             emit sslkey "$VALUE" ;;
        sslpassword)                        emit sslpassword "$VALUE" ;;
        rootcertificate)
            SSLROOTCERT_SET=1
            emit sslrootcert "$VALUE"
            ;;
        kerberosservicename)                emit krbsrvname "$VALUE" ;;
        channelbinding)                     emit channel_binding "$(to_lower "$VALUE")" ;;

        # --- Routing ---
        targetsessionattributes)
            # Assign first so `set -e` picks up a non-zero subshell exit.
            # Command substitution exit status is swallowed when embedded
            # directly as a function argument (`emit x "$(fails)"`) but
            # propagates through bare assignment.
            TSA=$(translate_target_session_attrs "$VALUE")
            emit target_session_attrs "$TSA"
            ;;
        loadbalancehosts)
            # Npgsql takes a bool; libpq takes a textual mode:
            #   true  -> random  (shuffle the host list)
            #   false -> disable (try in provided order, the libpq default)
            # Treating this as a bool 0/1 would either be rejected by libpq
            # or silently interpreted as "disable" for both values, losing
            # the balancing intent entirely.
            LBH_BOOL=$(parse_bool "$VALUE")
            case "$LBH_BOOL" in
                1) emit load_balance_hosts "random" ;;
                0) emit load_balance_hosts "disable" ;;
            esac
            ;;

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
        tcpkeepalive)
            KA=$(parse_bool "$VALUE")
            emit keepalives "$KA"
            ;;
        # Npgsql "TCP Keepalive Time" / "TCP Keepalive Interval" are
        # seconds, matching libpq's keepalives_idle / keepalives_interval.
        # No unit conversion needed — just validate it's an integer.
        tcpkeepalivetime)
            KAT=$(require_integer_seconds "$VALUE")
            emit keepalives_idle "$KAT"
            ;;
        tcpkeepaliveinterval)
            KAI=$(require_integer_seconds "$VALUE")
            emit keepalives_interval "$KAI"
            ;;

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

# Default sslrootcert=system for verify-{ca,full} when the caller didn't
# specify one. See comment near the SSLMODE_FINAL declaration above.
if [[ "$SSLROOTCERT_SET" -eq 0 ]] \
        && { [[ "$SSLMODE_FINAL" == "verify-ca" ]] || [[ "$SSLMODE_FINAL" == "verify-full" ]]; }; then
    OUT+="sslrootcert='system' "
    echo "Info: sslmode=$SSLMODE_FINAL set with no explicit Root Certificate — defaulting sslrootcert=system (libpq 16+) to mirror .NET's OS-trust-store behaviour." >&2
fi

OUT="${OUT% }"

if [[ "$OUT" != *"host="* ]] || [[ "$OUT" != *"dbname="* ]]; then
    echo "Error: converted connection string is missing host or dbname." >&2
    exit 1
fi

printf '%s\n' "$OUT"
