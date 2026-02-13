UPDATE public.mt_doc_inboundorder SET data = jsonb_set(data, '{Status}', '"Pending"') WHERE data->>'OrderNumber' = 'COLON/IN/00722';
