UPDATE public.mt_doc_inboundorder 
SET data = jsonb_set(
    data, 
    '{Lines}', 
    (
        SELECT jsonb_agg(
            CASE 
                WHEN elem->>'Sku' = '21481-JE30A' THEN 
                    jsonb_set(elem, '{OdooMoveId}', '505090'::jsonb)
                ELSE elem 
            END
        )
        FROM jsonb_array_elements(data->'Lines') as elem
    )
)
WHERE data->>'OrderNumber' = 'COLON/IN/00722';
