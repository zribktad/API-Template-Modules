CREATE FUNCTION claim_retryable_failed_emails(
    p_max_retry_attempts INT,
    p_batch_size INT,
    p_claimed_by TEXT,
    p_claimed_at_utc TIMESTAMPTZ,
    p_claimed_until_utc TIMESTAMPTZ
)
RETURNS TABLE(
    "Id" UUID,
    "To" TEXT,
    "Subject" TEXT,
    "HtmlBody" TEXT,
    "RetryCount" INT,
    "CreatedAtUtc" TIMESTAMPTZ,
    "LastAttemptAtUtc" TIMESTAMPTZ,
    "LastError" TEXT,
    "TemplateName" TEXT,
    "IsDeadLettered" BOOLEAN,
    "ClaimedBy" TEXT,
    "ClaimedAtUtc" TIMESTAMPTZ,
    "ClaimedUntilUtc" TIMESTAMPTZ
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH claimed AS (
        SELECT fe."Id"
        FROM "FailedEmails" fe
        WHERE NOT fe."IsDeadLettered"
          AND fe."RetryCount" < p_max_retry_attempts
          AND (fe."ClaimedUntilUtc" IS NULL OR fe."ClaimedUntilUtc" < p_claimed_at_utc)
        ORDER BY COALESCE(fe."LastAttemptAtUtc", fe."CreatedAtUtc")
        LIMIT p_batch_size
        FOR UPDATE SKIP LOCKED
    )
    UPDATE "FailedEmails" AS failed
    SET "ClaimedBy" = p_claimed_by,
        "ClaimedAtUtc" = p_claimed_at_utc,
        "ClaimedUntilUtc" = p_claimed_until_utc
    FROM claimed
    WHERE failed."Id" = claimed."Id"
    RETURNING failed."Id", failed."To", failed."Subject", failed."HtmlBody",
              failed."RetryCount", failed."CreatedAtUtc", failed."LastAttemptAtUtc",
              failed."LastError", failed."TemplateName", failed."IsDeadLettered",
              failed."ClaimedBy", failed."ClaimedAtUtc", failed."ClaimedUntilUtc";
END;
$$;
