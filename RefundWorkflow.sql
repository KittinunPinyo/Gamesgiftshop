-- Run once on an existing database with the refundrequests table.
-- Existing requests remain valid without an evidence image.
ALTER TABLE refundrequests
    ADD COLUMN ReturnAddress varchar(1000) NULL,
    ADD COLUMN ReturnTrackingNumber varchar(100) NULL;

CREATE TABLE refundevidence (
    RefundRequestId int NOT NULL,
    Data longblob NOT NULL,
    ContentType varchar(20) NOT NULL,
    PRIMARY KEY (RefundRequestId),
    CONSTRAINT refundevidence_ibfk_1 FOREIGN KEY (RefundRequestId)
        REFERENCES refundrequests (RefundRequestId) ON DELETE CASCADE
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
