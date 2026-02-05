-- Test query to check if Adaugotest user exists and has correct data
SELECT 
    username,
    email,
    profilestatus,
    pinstatus,
    CASE 
        WHEN transactionpin IS NOT NULL THEN 'PIN EXISTS' 
        ELSE 'NO PIN' 
    END as pin_status,
    CASE 
        WHEN bvn IS NOT NULL THEN 'BVN EXISTS' 
        ELSE 'NO BVN' 
    END as bvn_status
FROM OmniProfiles 
WHERE username = 'Adaugotest';

-- Also check for case sensitivity issues
SELECT 
    username,
    email,
    profilestatus,
    pinstatus
FROM OmniProfiles 
WHERE LOWER(username) = LOWER('Adaugotest');