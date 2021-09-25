const colors = require('tailwindcss/colors');
module.exports = {
    purge: {
        enabled: true,
        content: [
            './**/*.html',
            './**/*.razor'
        ],
    },
    darkMode: "class",
    variants: {
        extend: {opacity:['disabled']},
    },
    plugins: [require('@tailwindcss/forms')],
}