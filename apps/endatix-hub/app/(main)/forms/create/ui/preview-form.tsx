'use client'

import { ICreatorOptions } from 'survey-creator-core'
import { SurveyCreatorComponent, SurveyCreator } from 'survey-creator-react'
import 'survey-core/defaultV2.css'
import 'survey-creator-core/survey-creator-core.css'
import { useEffect, useState } from 'react'

interface PreviewFormProps {
    model: any;
}

const creatorOptions: ICreatorOptions = {
    showPreview: true,
    showTranslationTab: false,
    showDesignerTab: false
};

const PreviewForm = ({ model }: PreviewFormProps) => {
    const [creator, setCreator] = useState<SurveyCreator | null>(null);
    useEffect(() => {
        if (creator) {
            creator.JSON = model;
            return;
        }

        const newCreator = new SurveyCreator(creatorOptions);
        newCreator.JSON = model;
        newCreator.saveSurveyFunc = (no: number, callback: (num: number, status: boolean) => void) => {
            console.log(JSON.stringify(newCreator?.JSON));
            callback(no, true);
        };
        setCreator(newCreator);
    }, [model]);

    return (
        creator && <SurveyCreatorComponent creator={creator} />
    );
}

export default PreviewForm; 