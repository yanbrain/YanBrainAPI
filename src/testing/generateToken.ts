import axios from 'axios';
import dotenv from 'dotenv';

dotenv.config();

const TEST_USER = {
    email: 'test@yanbrain.com',
    password: 'TestPassword123!'
};

const FIREBASE_API_KEY = process.env.FIREBASE_API_KEY || '';

async function generateToken(): Promise<string> {
    if (!FIREBASE_API_KEY) {
        throw new Error('FIREBASE_API_KEY not found in .env');
    }

    try {
        // Sign in with email/password
        const response = await axios.post(
            `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=${FIREBASE_API_KEY}`,
            {
                email: TEST_USER.email,
                password: TEST_USER.password,
                returnSecureToken: true
            }
        );

        return response.data.idToken;
    } catch (error: any) {
        // If user doesn't exist, create it
        if (error.response?.data?.error?.message?.includes('EMAIL_NOT_FOUND')) {
            const signUpResponse = await axios.post(
                `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${FIREBASE_API_KEY}`,
                {
                    email: TEST_USER.email,
                    password: TEST_USER.password,
                    returnSecureToken: true
                }
            );

            return signUpResponse.data.idToken;
        }

        throw new Error(error.response?.data?.error?.message || error.message);
    }
}

// Run standalone
if (require.main === module) {
    generateToken()
        .then((token) => {
            console.log(token);
            process.exit(0);
        })
        .catch((error) => {
            console.error('Error:', error.message);
            process.exit(1);
        });
}

export { generateToken };